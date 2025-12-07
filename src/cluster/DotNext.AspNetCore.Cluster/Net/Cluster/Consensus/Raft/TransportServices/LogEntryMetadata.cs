using System.Buffers;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

/// <summary>
/// Represents serializable log entry metadata that
/// can be passed over the wire using HTTP protocol.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata
{
    internal const int Size = sizeof(long) + sizeof(byte) + sizeof(int) + sizeof(long) + sizeof(long);
    private const byte NoFlags = 0;
    private const byte HasIdentifierFlag = 0x01;

    private readonly long timestamp;
    private readonly byte flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, DateTimeOffset timestamp, long length, int? identifier)
    {
        this.Term = term;
        this.timestamp = timestamp.UtcTicks;
        this.Length = length;
        flags = identifier.HasValue ? HasIdentifierFlag : NoFlags;
        this.identifier = identifier.GetValueOrDefault();
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : IRaftLogEntry
        => new(entry.Term, entry.Timestamp, entry.Length.GetValueOrDefault(), entry.CommandId);

    internal LogEntryMetadata(ReadOnlyMemory<byte> input)
        : this(new ReadOnlySequence<byte>(input), out _)
    {
    }

    internal LogEntryMetadata(ReadOnlySequence<byte> input, out SequencePosition position)
    {
        Debug.Assert(input.Length >= Size);

        var reader = new SequenceReader(input);
        Term = reader.ReadLittleEndian<long>();
        timestamp = reader.ReadLittleEndian<long>();
        flags = reader.ReadByte();
        identifier = reader.ReadLittleEndian<int>();
        Length = reader.ReadLittleEndian<long>();

        position = reader.Position;
    }

    internal long Term { get; }

    internal long Length { get; }

    internal DateTimeOffset Timestamp => new(timestamp, TimeSpan.Zero);

    internal int? CommandId => (flags & HasIdentifierFlag) != 0 ? identifier : null;

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(timestamp);
        writer += flags;
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(Length);
    }
}