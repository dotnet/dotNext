namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class AppendEntriesMessage
{
    internal static void Write(ref SpanWriter<byte> writer, long term, long prevLogIndex, long prevLogTerm, long commitIndex)
    {
        writer.WriteInt64(term, true);
        writer.WriteInt64(prevLogIndex, true);
        writer.WriteInt64(prevLogTerm, true);
        writer.WriteInt64(commitIndex, true);
    }
}