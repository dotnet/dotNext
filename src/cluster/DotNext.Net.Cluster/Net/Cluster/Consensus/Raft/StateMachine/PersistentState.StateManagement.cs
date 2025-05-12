using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers.Binary;

partial class PersistentState
{
    /// <summary>
    /// Instructs the underlying GC to remove any persistent data from the snapshot up to the specified index.
    /// </summary>
    /// <remarks>
    /// The infrastructure grants that there are no subsequent reads of the snapshot that can start at
    /// the position less than or equal to <see cref="upToIndex"/>.
    /// </remarks>
    /// <param name="upToIndex">The watermark of the snapshot, inclusive.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask ReclaimGarbageAsync(long upToIndex, CancellationToken token);

    protected abstract ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token);
    
    protected abstract SnapshotMetadata SnapshotInfo { get; }
    
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct SnapshotMetadata : IBinaryFormattable<SnapshotMetadata>
    {
        internal const int Size = sizeof(long) + LogEntryMetadata.Size;
        internal readonly long Index;
        internal readonly LogEntryMetadata RecordMetadata;

        private SnapshotMetadata(LogEntryMetadata metadata, long index)
        {
            Index = index;
            RecordMetadata = metadata;
        }

        internal SnapshotMetadata(ReadOnlySpan<byte> input)
        {
            Index = BinaryPrimitives.ReadInt64LittleEndian(input);
            RecordMetadata = new(input.Slice(sizeof(long)));
        }

        internal SnapshotMetadata(long index, DateTimeOffset timeStamp, long term, long length, int? id = null)
            : this(new LogEntryMetadata(timeStamp, term, Size, length, id), index)
        {
        }

        static SnapshotMetadata IBinaryFormattable<SnapshotMetadata>.Parse(ReadOnlySpan<byte> input) => new(input);

        static int IBinaryFormattable<SnapshotMetadata>.Size => Size;

        internal static SnapshotMetadata Create<TLogEntry>(TLogEntry snapshot, long index, long length)
            where TLogEntry : IRaftLogEntry
            => new(LogEntryMetadata.Create(snapshot, 0L, length), index);

        public void Format(Span<byte> output)
        {
            BinaryPrimitives.WriteInt64LittleEndian(output, Index);
            RecordMetadata.Format(output.Slice(sizeof(long)));
        }
    }
}