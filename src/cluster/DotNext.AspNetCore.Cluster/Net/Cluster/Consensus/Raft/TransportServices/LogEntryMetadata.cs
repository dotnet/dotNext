using System.Buffers;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using SequenceReader = IO.SequenceReader;

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

    private readonly long timestamp, term, length;
    private readonly byte flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, DateTimeOffset timestamp, long length, int? identifier)
    {
        this.term = term;
        this.timestamp = timestamp.UtcTicks;
        this.length = length;
        flags = identifier.HasValue ? HasIdentifierFlag : NoFlags;
        this.identifier = identifier.GetValueOrDefault();
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : notnull, IRaftLogEntry
        => new(entry.Term, entry.Timestamp, entry.Length.GetValueOrDefault(), entry.CommandId);

    internal LogEntryMetadata(ReadOnlyMemory<byte> input)
        : this(new ReadOnlySequence<byte>(input), out _)
    {
    }

    internal LogEntryMetadata(ReadOnlySequence<byte> input, out SequencePosition position)
    {
        Debug.Assert(input.Length >= Size);

        var reader = new SequenceReader(input);
        term = reader.ReadInt64(littleEndian: true);
        timestamp = reader.ReadInt64(littleEndian: true);
        flags = reader.Read<byte>();
        identifier = reader.ReadInt32(littleEndian: true);
        length = reader.ReadInt64(littleEndian: true);

        position = reader.Position;
    }

    internal long Term => term;

    internal long Length => length;

    internal DateTimeOffset Timestamp => new(timestamp, TimeSpan.Zero);

    internal int? CommandId => (flags & HasIdentifierFlag) != 0 ? identifier : null;

    internal void Serialize(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteInt64(Term, true);
        writer.WriteInt64(timestamp, true);
        writer.Add(flags);
        writer.WriteInt32(identifier, true);
        writer.WriteInt64(Length, true);
    }
}