using System;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_1
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;

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
            private readonly StreamSegment content;
            private readonly LogEntryMetadata metadata;
            private readonly Memory<byte> buffer;
            internal readonly long? SnapshotIndex;

            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in LogEntryMetadata metadata)
            {
                this.metadata = metadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = null;
            }

            internal LogEntry(StreamSegment cachedContent, Memory<byte> sharedBuffer, in SnapshotMetadata metadata)
            {
                this.metadata = metadata.RecordMetadata;
                content = cachedContent;
                buffer = sharedBuffer;
                SnapshotIndex = metadata.Index;
            }

            /// <summary>
            /// Gets a value indicating that this entry is a snapshot entry.
            /// </summary>
            public bool IsSnapshot => SnapshotIndex.HasValue;

            /// <summary>
            /// Gets length of the log entry content, in bytes.
            /// </summary>
            public long Length => metadata.Length;

            internal bool IsEmpty => metadata.Length == 0L;

            internal void Reset()
                => content.Adjust(metadata.Offset, Length);

            /// <inheritdoc/>
            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                Reset();
                return new ValueTask(writer.CopyFromAsync(content, token));
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
                Reset();
                return IDataTransferObject.TransformAsync<TResult, TTransformation>(content, transformation, false, buffer, token);
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
            public ValueTask<object?> DeserializeFromJsonAsync(Func<string, Type>? typeLoader = null, JsonSerializerOptions? options = null, CancellationToken token = default)
            {
                Reset();
                return JsonLogEntry.DeserializeAsync(content, typeLoader ?? JsonLogEntry.DefaultTypeLoader, options, token);
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
