namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class SnapshotMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + LogEntryMetadata.Size;

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long snapshotIndex, IRaftLogEntry snapshot)
    {
        id.Format(ref writer);
        writer.WriteLittleEndian(term);
        writer.WriteLittleEndian(snapshotIndex);
        LogEntryMetadata.Create(snapshot).Format(ref writer);
    }

    internal static (ClusterMemberId Id, long Term, long SnapshotIndex, LogEntryMetadata SnapshotMetadata) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadLittleEndian<long>(isUnsigned: false),
        SnapshotIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        SnapshotMetadata = new(ref reader),
    };
}