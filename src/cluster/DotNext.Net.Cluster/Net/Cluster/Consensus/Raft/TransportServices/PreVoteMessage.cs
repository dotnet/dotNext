namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class PreVoteMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + sizeof(long);

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long lastLogIndex, long lastLogTerm)
    {
        id.Format(ref writer);
        writer.WriteInt64(term, true);
        writer.WriteInt64(lastLogIndex, true);
        writer.WriteInt64(lastLogTerm, true);
    }

    internal static (ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadInt64(true),
        LastLogIndex = reader.ReadInt64(true),
        LastLogTerm = reader.ReadInt64(true),
    };
}