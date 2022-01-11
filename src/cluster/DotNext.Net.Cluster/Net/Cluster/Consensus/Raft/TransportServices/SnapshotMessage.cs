namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class SnapshotMessage
{
    internal const int Size = sizeof(long) + sizeof(long) + LogEntryMetadata.Size;

    internal static void Write(ref SpanWriter<byte> writer, long term, long snapshotIndex, IRaftLogEntry snapshot)
    {
        writer.WriteInt64(term, true);
        writer.WriteInt64(snapshotIndex, true);
        LogEntryMetadata.Create(snapshot).Format(ref writer);
    }

    internal static (long Term, long SnapshotIndex, LogEntryMetadata SnapshotMetadata) Read(ref SpanReader<byte> reader) => new()
    {
        Term = reader.ReadInt64(true),
        SnapshotIndex = reader.ReadInt64(true),
        SnapshotMetadata = new(ref reader),
    };
}