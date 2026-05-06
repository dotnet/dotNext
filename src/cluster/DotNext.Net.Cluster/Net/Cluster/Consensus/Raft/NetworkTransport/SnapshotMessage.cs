using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

using Buffers;
using Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct SnapshotMessage(ClusterMemberId Id, long Term, long SnapshotIndex, LogEntryMetadata Metadata,
    long ConfigurationVersion) : IBinaryFormattable<SnapshotMessage>
{
    public static int Size => ClusterMemberId.Size + sizeof(long) + sizeof(long) + LogEntryMetadata.Size + sizeof(long);

    internal SnapshotMessage(ClusterMemberId id, long term, long snapshotIndex, IRaftLogEntry snapshot, long configurationVersion)
        : this(id, term, snapshotIndex, LogEntryMetadata.Create(snapshot), configurationVersion)
    {
    }

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.Write(Id);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(SnapshotIndex);
        writer.Write(Metadata);
        writer.WriteLittleEndian(ConfigurationVersion);
    }

    public static SnapshotMessage Parse(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return new(
            reader.Read<ClusterMemberId>(),
            reader.ReadLittleEndian<long>(),
            reader.ReadLittleEndian<long>(),
            reader.Read<LogEntryMetadata>(),
            reader.ReadLittleEndian<long>());
    }
}