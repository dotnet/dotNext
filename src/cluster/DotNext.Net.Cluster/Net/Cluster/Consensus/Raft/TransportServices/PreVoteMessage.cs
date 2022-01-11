namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class PreVoteMessage
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long);

    internal static void Write(ref SpanWriter<byte> writer, long term, long lastLogIndex, long lastLogTerm)
    {
        writer.WriteInt64(term, true);
        writer.WriteInt64(lastLogIndex, true);
        writer.WriteInt64(lastLogTerm, true);
    }

    internal static (long Term, long LastLogIndex, long LastLogTerm) Read(ref SpanReader<byte> reader) => new()
    {
        Term = reader.ReadInt64(true),
        LastLogIndex = reader.ReadInt64(true),
        LastLogTerm = reader.ReadInt64(true),
    };
}