using System;
using System.Runtime.InteropServices;
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
        }
    }
}
