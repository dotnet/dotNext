namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class HeartbeatMessage
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + EmptyClusterConfiguration.Size;

    internal static void Write(ref SpanWriter<byte> writer, long term, long prevLogIndex, long prevLogTerm, long commitIndex, in EmptyClusterConfiguration? configuration)
    {
        writer.WriteInt64(term, true);
        writer.WriteInt64(prevLogIndex, true);
        writer.WriteInt64(prevLogTerm, true);
        writer.WriteInt64(commitIndex, true);
        EmptyClusterConfiguration.WriteTo(in configuration, ref writer);
    }

    internal static (long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, EmptyClusterConfiguration? Configuration) Read(ref SpanReader<byte> reader) => new()
    {
        Term = reader.ReadInt64(true),
        PrevLogIndex = reader.ReadInt64(true),
        PrevLogTerm = reader.ReadInt64(true),
        CommitIndex = reader.ReadInt64(true),
        Configuration = EmptyClusterConfiguration.ReadFrom(ref reader),
    };
}