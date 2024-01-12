using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;
using BitVector = Numerics.BitVector;

[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(byte) + sizeof(int);
    private const byte IdentifierFlag = 0x01;
    private const byte SnapshotFlag = IdentifierFlag << 1;

    internal readonly long Term;
    private readonly long length, timestamp;
    private readonly byte flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, DateTimeOffset timestamp, bool isSnapshot, int? commandId, long? length)
    {
        Term = term;
        this.timestamp = timestamp.UtcTicks;
        flags = BitVector.FromBits<byte>([commandId.HasValue, isSnapshot]);
        identifier = commandId.GetValueOrDefault();
        this.length = length.GetValueOrDefault(-1L);
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : notnull, IRaftLogEntry
        => new(entry.Term, entry.Timestamp, entry.IsSnapshot, entry.CommandId, entry.Length);

    internal LogEntryMetadata(ref SpanReader<byte> reader)
    {
        Term = reader.ReadLittleEndian<long>();
        timestamp = reader.ReadLittleEndian<long>();
        flags = reader.Read();
        identifier = reader.ReadLittleEndian<int>();
        length = reader.ReadLittleEndian<long>();
    }

    internal LogEntryMetadata(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        this = new(ref reader);
    }

    static LogEntryMetadata IBinaryFormattable<LogEntryMetadata>.Parse(ReadOnlySpan<byte> input)
        => new(input);

    static int IBinaryFormattable<LogEntryMetadata>.Size => Size;

    internal DateTimeOffset Timestamp => new(timestamp, TimeSpan.Zero);

    internal long? Length => length >= 0L ? length : null;

    internal int? CommandId => (flags & IdentifierFlag) is not 0 ? identifier : null;

    internal bool IsSnapshot => (flags & SnapshotFlag) is not 0;

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(timestamp);
        writer.Add(flags);
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(length);
    }
}