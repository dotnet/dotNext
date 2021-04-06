using System;
using System.IO;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_1
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;
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
        protected readonly struct LogEntry : IRaftLogEntry
        {
            private readonly StreamSegment? content;
            private readonly LogEntryMetadata metadata;
            private readonly Memory<byte> buffer;

            // if negative then it's a snapshot index because |snapshotIndex| > 0
            private readonly long index;

            // for regular log entry
            internal LogEntry(StreamSegment? cachedContent, Memory<byte> sharedBuffer, in LogEntryMetadata metadata, long index)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                this.index = index;
            }

            // for snapshot
            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in SnapshotMetadata metadata)
            {
                Debug.Assert(metadata.Index > 0L);
                this.metadata = metadata.RecordMetadata;
                content = cachedContent;
                buffer = sharedBuffer;
                index = -metadata.Index;
            }

            // for ephemeral entry
            internal LogEntry(Memory<byte> sharedBuffer)
            {
                metadata = default;
                content = null;
                buffer = sharedBuffer;
                index = 0L;
            }

            internal long? SnapshotIndex
            {
                get
                {
                    var i = -index;
                    return i > 0L ? i : null;
                }
            }

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

            internal void Reset()
                => content?.Adjust(metadata.Offset, Length);

            /// <inheritdoc/>
            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                Task result;
                if (content is null)
                {
                    result = Task.CompletedTask;
                }
                else
                {
                    Reset();
                    result = writer.CopyFromAsync(content, token);
                }

                return new ValueTask(result);
            }

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

            /// <inheritdoc/>
            public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
            {
                Stream source;
                if (content is null)
                {
                    source = Stream.Null;
                }
                else
                {
                    Reset();
                    source = content;
                }

                return IDataTransferObject.TransformAsync<TResult, TTransformation>(source, transformation, false, buffer, token);
            }

            /// <summary>
            /// Gets reader that can be used to deserialize the content of this log entry.
            /// </summary>
            /// <returns>The binary reader providing access to the content of this log entry.</returns>
            public IAsyncBinaryReader GetReader()
            {
                if (content is null)
                    return IAsyncBinaryReader.Empty;

                Reset();
                return IAsyncBinaryReader.Create(content, buffer);
            }

#if !NETSTANDARD2_1
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
            public ValueTask<object?> DeserializeFromJsonAsync(Func<string, Type>? typeLoader = null, JsonSerializerOptions? options = null, CancellationToken token = default)
            {
                Stream source;
                if (content is null)
                {
                    source = Stream.Null;
                }
                else
                {
                    Reset();
                    source = content;
                }

                return JsonLogEntry.DeserializeAsync(source, typeLoader ?? JsonLogEntry.DefaultTypeLoader, options, token);
            }
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
