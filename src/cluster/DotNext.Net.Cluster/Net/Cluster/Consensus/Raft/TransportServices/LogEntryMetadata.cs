using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using BitVector = Numerics.BitVector;

[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(byte) + sizeof(int);
    private const byte IdentifierFlag = 0x01;
    private const byte SnapshotFlag = IdentifierFlag << 1;

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
        Term = reader.ReadLittleEndian<long>(isUnsigned: false);
        timestamp = reader.ReadLittleEndian<long>(isUnsigned: false);
        flags = reader.Read();
        identifier = reader.ReadLittleEndian<int>(isUnsigned: false);
        length = reader.ReadLittleEndian<long>(isUnsigned: false);
    }

    static int IBinaryFormattable<LogEntryMetadata>.Size => Size;

    public static LogEntryMetadata Parse(ref SpanReader<byte> input) => new(ref input);

    internal long Term { get; }

    internal DateTimeOffset Timestamp => new(timestamp, TimeSpan.Zero);

    internal long? Length => length >= 0L ? length : null;

    internal int? CommandId => (flags & IdentifierFlag) is not 0 ? identifier : null;

    internal bool IsSnapshot => (flags & SnapshotFlag) is not 0;

    public void Format(ref SpanWriter<byte> writer)
    {
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(timestamp);
        writer.Add(flags);
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(length);
    }
}