namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class AppendEntriesMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(int);

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long prevLogIndex, long prevLogTerm, long commitIndex, int entriesCount)
    {
        id.Format(ref writer);
        writer.WriteInt64(term, true);
        writer.WriteInt64(prevLogIndex, true);
        writer.WriteInt64(prevLogTerm, true);
        writer.WriteInt64(commitIndex, true);
        writer.WriteInt32(entriesCount, true);
    }

    internal static (ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadInt64(true),
        PrevLogIndex = reader.ReadInt64(true),
        PrevLogTerm = reader.ReadInt64(true),
        CommitIndex = reader.ReadInt64(true),
        EntriesCount = reader.ReadInt32(true),
    };
}