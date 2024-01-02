using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct VoteMessage(ClusterMemberId Id, long Term, long LastLogIndex, long LastLogTerm) : IBinaryFormattable<VoteMessage>
{
    public static int Size => sizeof(long) + sizeof(long) + sizeof(long) + ClusterMemberId.Size;

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.Write(Id);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(LastLogIndex);
        writer.WriteLittleEndian(LastLogTerm);
    }

    public static VoteMessage Parse(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return new(
            reader.Read<ClusterMemberId>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>());
    }
}