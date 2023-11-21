namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class AppendEntriesMessage
{
    internal static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(int);

    internal static void Write(ref SpanWriter<byte> writer, in ClusterMemberId id, long term, long prevLogIndex, long prevLogTerm, long commitIndex, int entriesCount)
    {
        id.Format(ref writer);
        writer.WriteLittleEndian(term);
        writer.WriteLittleEndian(prevLogIndex);
        writer.WriteLittleEndian(prevLogTerm);
        writer.WriteLittleEndian(commitIndex);
        writer.WriteLittleEndian(entriesCount);
    }

    internal static (ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount) Read(ref SpanReader<byte> reader) => new()
    {
        Id = new(ref reader),
        Term = reader.ReadLittleEndian<long>(isUnsigned: false),
        PrevLogIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        PrevLogTerm = reader.ReadLittleEndian<long>(isUnsigned: false),
        CommitIndex = reader.ReadLittleEndian<long>(isUnsigned: false),
        EntriesCount = reader.ReadLittleEndian<int>(isUnsigned: false),
    };
}