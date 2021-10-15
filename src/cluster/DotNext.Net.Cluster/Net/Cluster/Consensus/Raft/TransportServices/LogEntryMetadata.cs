using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using BitVector = Numerics.BitVector;

#pragma warning disable CA2252  // TODO: Remove in .NET 7
[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(byte) + sizeof(int);
    private const byte IdentifierFlag = 0x01;
    private const byte SnapshotFlag = IdentifierFlag << 1;

    private readonly long length, timestamp;
    private readonly byte flags;
    private readonly int identifier;

    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500", Justification = "False positive")]
    private LogEntryMetadata(long term, DateTimeOffset timestamp, bool isSnapshot, int? commandId, long? length)
    {
        Term = term;
        this.timestamp = timestamp.UtcTicks;
        flags = BitVector.ToByte(stackalloc bool[] { commandId.HasValue, isSnapshot });
        identifier = commandId.GetValueOrDefault();
        this.length = length.GetValueOrDefault(-1L);
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : notnull, IRaftLogEntry
        => new(entry.Term, entry.Timestamp, entry.IsSnapshot, entry.CommandId, entry.Length);

    internal LogEntryMetadata(ref SpanReader<byte> reader)
    {
        Term = reader.ReadInt64(true);
        timestamp = reader.ReadInt64(true);
        flags = reader.Read();
        identifier = reader.ReadInt32(true);
        length = reader.ReadInt64(true);
    }

    static int IBinaryFormattable<LogEntryMetadata>.Size => Size;

    static LogEntryMetadata IBinaryFormattable<LogEntryMetadata>.Parse(ref SpanReader<byte> input)
        => new(ref input);

    internal long Term { get; }

    internal DateTimeOffset Timestamp => new(timestamp, TimeSpan.Zero);

    internal long? Length => length >= 0L ? length : null;

    internal int? CommandId => (flags & IdentifierFlag) != 0 ? identifier : null;

    internal bool IsSnapshot => (flags & SnapshotFlag) != 0;

    public void Format(ref SpanWriter<byte> writer)
    {
        writer.WriteInt64(Term, true);
        writer.WriteInt64(timestamp, true);
        writer.Add(flags);
        writer.WriteInt32(identifier, true);
        writer.WriteInt64(length, true);
    }
}
#pragma warning restore CA2252