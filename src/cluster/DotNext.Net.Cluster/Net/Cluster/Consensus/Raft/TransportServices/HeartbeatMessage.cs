namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class HeartbeatMessage
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + EmptyClusterConfiguration.Size;

    internal static void Write(ref SpanWriter<byte> writer, long term, long prevLogIndex, long prevLogTerm, long commitIndex, in EmptyClusterConfiguration? configuration)
    {
        writer.WriteLittleEndian(term);
        writer.WriteLittleEndian(prevLogIndex);
        writer.WriteLittleEndian(prevLogTerm);
        writer.WriteLittleEndian(commitIndex);
        EmptyClusterConfiguration.WriteTo(in configuration, ref writer);
    }

    internal static (long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, EmptyClusterConfiguration? Configuration) Read(ref SpanReader<byte> reader) => new()
    {
        Term = reader.ReadLittleEndian<long>(isUnsigned: false),
        PrevLogIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        PrevLogTerm = reader.ReadLittleEndian<long>(isUnsigned: false),
        CommitIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        Configuration = EmptyClusterConfiguration.ReadFrom(ref reader),
    };
}