namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class PreVoteMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + sizeof(long);

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long lastLogIndex, long lastLogTerm)
    {
        id.Format(ref writer);
        writer.WriteLittleEndian(term);
        writer.WriteLittleEndian(lastLogIndex);
        writer.WriteLittleEndian(lastLogTerm);
    }

    internal static (ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadLittleEndian<long>(isUnsigned: false),
        LastLogIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        LastLogTerm = reader.ReadLittleEndian<long>(isUnsigned: false),
    };
}