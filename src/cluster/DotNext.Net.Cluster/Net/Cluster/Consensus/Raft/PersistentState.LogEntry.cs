using System;
using System.Buffers;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_1
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.Unsafe;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;

    public partial class PersistentState
    {
        /// <summary>
        /// Represents persistent log entry.
        /// </summary>
        /// <remarks>
        /// Use <see cref="TransformAsync"/> to decode the log entry.
        /// </remarks>
        [StructLayout(LayoutKind.Auto)]
        protected internal readonly struct LogEntry : IRaftLogEntry
        {
            private enum ContentType : byte
            {
                None = 0,
                Stream,
                Memory,
            }

            // this field has correlation with 'content' field
            private readonly ContentType contentType;
            private readonly IDisposable? content;
            private readonly LogEntryMetadata metadata;
            private readonly Memory<byte> buffer;

            // if negative then it's a snapshot index because |snapshotIndex| > 0
            private readonly long index;

            // for regular log entry
            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in LogEntryMetadata metadata, long index)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                this.index = index;
                contentType = ContentType.Stream;
            }

            // for regular log entry cached in memory
            internal LogEntry(IMemoryOwner<byte> cachedContent, in LogEntryMetadata metadata, long index)
            {
                Debug.Assert(cachedContent.Memory.Length == metadata.Length);
                this.metadata = metadata;
                content = cachedContent;
                buffer = default;
                this.index = index;
                contentType = ContentType.Memory;
            }

            // for snapshot
            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in SnapshotMetadata metadata)
            {
                Debug.Assert(metadata.Index > 0L);
                this.metadata = metadata.RecordMetadata;
                content = cachedContent;
                buffer = sharedBuffer;
                index = -metadata.Index;
                contentType = ContentType.Stream;
            }

            // for ephemeral entry
            internal LogEntry(Memory<byte> sharedBuffer)
            {
                metadata = default;
                content = null;
                buffer = sharedBuffer;
                index = 0L;
                contentType = ContentType.None;
            }

            internal long? SnapshotIndex
            {
                get
                {
                    var i = -index;
                    return i > 0L ? i : null;
                }
            }

            internal bool IsBuffered => contentType == ContentType.Memory;

            /// <summary>
            /// Gets the index of this log entry.
            /// </summary>
            public long Index => Math.Abs(index);

            /// <summary>
            /// Gets identifier of the command encapsulated by this log entry.
            /// </summary>
            public int? CommandId => metadata.Id;

            /// <summary>
            /// Gets a value indicating that this entry is a snapshot entry.
            /// </summary>
            public bool IsSnapshot => index < 0L;

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            internal bool IsEmpty => Length == 0L;

            private static void Adjust(StreamSegment segment, in LogEntryMetadata metadata)
                => segment.Adjust(metadata.Offset, metadata.Length);

            private ValueTask CopyFromStream<TWriter>(TWriter writer, CancellationToken token)
                where TWriter : notnull, IAsyncBinaryWriter
            {
                Debug.Assert(content is StreamSegment);
                var segment = As<StreamSegment>(content);
                Adjust(segment, in metadata);
                return new (writer.CopyFromAsync(segment, token));
            }

            private ValueTask CopyFromMemory<TWriter>(TWriter writer, CancellationToken token)
                where TWriter : notnull, IAsyncBinaryWriter
            {
                Debug.Assert(content is IMemoryOwner<byte>);
                return writer.WriteAsync(As<IMemoryOwner<byte>>(content).Memory, null, token);
            }

            /// <inheritdoc/>
            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token) => contentType switch
            {
                ContentType.Stream => CopyFromStream(writer, token),
                ContentType.Memory => CopyFromMemory(writer, token),
                _ => new (),
            };

            /// <inheritdoc/>
            long? IDataTransferObject.Length => Length;

            /// <inheritdoc/>
            bool IDataTransferObject.IsReusable => false;

            /// <summary>
            /// Gets Raft term of this log entry.
            /// </summary>
            public long Term => metadata.Term;

            /// <summary>
            /// Gets timestamp of this log entry.
            /// </summary>
            public DateTimeOffset Timestamp => new DateTimeOffset(metadata.Timestamp, TimeSpan.Zero);

            private ValueTask<TResult> TransformStreamAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
            {
                Debug.Assert(content is StreamSegment);
                var segment = As<StreamSegment>(content);
                Adjust(segment, in metadata);
                return IDataTransferObject.TransformAsync<TResult, TTransformation>(segment, transformation, false, buffer, token);
            }

            private ValueTask<TResult> TransformMemoryAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
                => transformation.TransformAsync(GetMemoryReader(), token);

            /// <inheritdoc/>
            public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
                => contentType switch
                {
                    ContentType.Stream => TransformStreamAsync<TResult, TTransformation>(transformation, token),
                    ContentType.Memory => TransformMemoryAsync<TResult, TTransformation>(transformation, token),
                    _ => transformation.TransformAsync(IAsyncBinaryReader.Empty, token),
                };

            private IAsyncBinaryReader GetStreamReader()
            {
                Debug.Assert(content is StreamSegment);
                var segment = As<StreamSegment>(content);
                Adjust(segment, in metadata);
                return IAsyncBinaryReader.Create(segment, buffer);
            }

            private SequenceBinaryReader GetMemoryReader()
            {
                Debug.Assert(content is IMemoryOwner<byte>);
                return IAsyncBinaryReader.Create(As<IMemoryOwner<byte>>(content).Memory);
            }

            /// <summary>
            /// Gets reader that can be used to deserialize the content of this log entry.
            /// </summary>
            /// <returns>The binary reader providing access to the content of this log entry.</returns>
            public IAsyncBinaryReader GetReader() => contentType switch
            {
                ContentType.Stream => GetStreamReader(),
                ContentType.Memory => GetMemoryReader(),
                _ => IAsyncBinaryReader.Empty,
            };

#if !NETSTANDARD2_1
            private ValueTask<object?> DeserializeFromJsonStreamAsync(Func<string, Type>? typeLoader, JsonSerializerOptions? options, CancellationToken token)
            {
                Debug.Assert(content is StreamSegment);
                var segment = As<StreamSegment>(content);
                Adjust(segment, in metadata);
                return JsonLogEntry.DeserializeAsync(segment, typeLoader, options, token);
            }

            /// <summary>
            /// Deserializes JSON content represented by this log entry.
            /// </summary>
            /// <param name="typeLoader">
            /// The type loader responsible for resolving the type to be deserialized.
            /// If <see langword="null"/> then <see cref="Type.GetType(string, bool)"/> is used
            /// for type resolution.
            /// </param>
            /// <param name="options">Deserialization options.</param>
            /// <param name="token">The token that can be used to cancel the deserialization.</param>
            /// <returns>The deserialized object.</returns>
            /// <exception cref="TypeLoadException"><paramref name="typeLoader"/> unable to resolve the type.</exception>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <seealso cref="CreateJsonLogEntry"/>
            public ValueTask<object?> DeserializeFromJsonAsync(Func<string, Type>? typeLoader = null, JsonSerializerOptions? options = null, CancellationToken token = default) => contentType switch
            {
                ContentType.Stream => DeserializeFromJsonStreamAsync(typeLoader, options, token),
                ContentType.Memory => new (JsonLogEntry.Deserialize(GetMemoryReader(), typeLoader, options)),
                _ => new (default(object)),
            };
#endif
        }

#if !NETSTANDARD2_1
        /// <summary>
        /// Creates a log entry with JSON-serializable payload.
        /// </summary>
        /// <typeparam name="T">JSON-serializable type.</typeparam>
        /// <param name="content">JSON-serializable content of the log entry.</param>
        /// <param name="typeId">
        /// The type identifier required to recognize the correct type during deserialization.
        /// If <see langword="null"/> then <see cref="Type.AssemblyQualifiedName"/> of <typeparamref name="T"/> is used as type identifier.
        /// </param>
        /// <param name="options">Serialization options.</param>
        /// <returns>The log entry representing JSON-serializable content.</returns>
        /// <seealso cref="LogEntry.DeserializeFromJsonAsync"/>
        public JsonLogEntry<T> CreateJsonLogEntry<T>(T content, string? typeId = null, JsonSerializerOptions? options = null)
            => new JsonLogEntry<T>(Term, content, typeId, options);
#endif
    }
}
