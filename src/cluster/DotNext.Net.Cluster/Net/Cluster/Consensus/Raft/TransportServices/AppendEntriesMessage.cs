using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct AppendEntriesMessage(ClusterMemberId Id, long Term, long PrevLogIndex, long PrevLogTerm, long CommitIndex, int EntriesCount) : IBinaryFormattable<AppendEntriesMessage>
{
    public static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(int);

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.Write(Id);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(PrevLogIndex);
        writer.WriteLittleEndian(PrevLogTerm);
        writer.WriteLittleEndian(CommitIndex);
        writer.WriteLittleEndian(EntriesCount);
    }

    public static AppendEntriesMessage Parse(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return new(
            reader.Read<ClusterMemberId>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<int>());
    }
}