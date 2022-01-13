namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class SnapshotMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + LogEntryMetadata.Size;

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long snapshotIndex, IRaftLogEntry snapshot)
    {
        id.Format(ref writer);
        writer.WriteInt64(term, true);
        writer.WriteInt64(snapshotIndex, true);
        LogEntryMetadata.Create(snapshot).Format(ref writer);
    }

    internal static (ClusterMemberId Id, long Term, long SnapshotIndex, LogEntryMetadata SnapshotMetadata) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadInt64(true),
        SnapshotIndex = reader.ReadInt64(true),
        SnapshotMetadata = new(ref reader),
    };
}