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
    public const int Size = sizeof(long) + sizeof(byte) + sizeof(int) + sizeof(long);
    private const byte NoFlags = 0;
    private const byte HasIdentifierFlag = 0x01;

    public readonly long Length, Term;
    private readonly byte flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, long length, int? identifier)
    {
        Term = term;
        Length = length;
        flags = identifier.HasValue ? HasIdentifierFlag : NoFlags;
        this.identifier = identifier.GetValueOrDefault();
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : IRaftLogEntry
        => new(entry.Term, entry.Length.GetValueOrDefault(), entry.CommandId);

    internal LogEntryMetadata(ReadOnlyMemory<byte> input)
        : this(new ReadOnlySequence<byte>(input), out _)
    {
    }

    internal LogEntryMetadata(ReadOnlySequence<byte> input, out SequencePosition position)
    {
        Debug.Assert(input.Length >= Size);

        var reader = new SequenceReader(input);
        Term = reader.ReadLittleEndian<long>();
        flags = reader.ReadByte();
        identifier = reader.ReadLittleEndian<int>();
        Length = reader.ReadLittleEndian<long>();

        position = reader.Position;
    }

    internal int? CommandId => (flags & HasIdentifierFlag) != 0 ? identifier : null;

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);
        writer += flags;
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(Length);
    }
}