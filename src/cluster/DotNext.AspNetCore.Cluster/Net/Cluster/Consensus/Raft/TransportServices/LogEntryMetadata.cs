using System.Buffers;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

using Buffers;

/// <summary>
/// Represents serializable log entry metadata that
/// can be passed over the wire using HTTP protocol.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata
{
    public const int Size = sizeof(long) + sizeof(Flags) + sizeof(int) + sizeof(long);

    public readonly long Length, Term;
    private readonly Flags flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, long length)
    {
        Term = term;
        Length = length;
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry)
        where TEntry : IRaftLogEntry
        => new(entry.Term, entry.Length.GetValueOrDefault()) { CommandId = entry.CommandId, IsConfiguration = entry.IsConfiguration };

    internal LogEntryMetadata(ReadOnlyMemory<byte> input)
        : this(new ReadOnlySequence<byte>(input), out _)
    {
    }

    internal LogEntryMetadata(ReadOnlySequence<byte> input, out SequencePosition position)
    {
        Debug.Assert(input.Length >= Size);

        var reader = new SequenceReader(input);
        Term = reader.ReadLittleEndian<long>();
        flags = (Flags)reader.ReadByte();
        identifier = reader.ReadLittleEndian<int>();
        Length = reader.ReadLittleEndian<long>();

        position = reader.Position;
    }

    internal int? CommandId
    {
        get => (flags & Flags.HasIdentifier) is not 0 ? identifier : null;
        private init
        {
            flags |= value.HasValue ? Flags.HasIdentifier : Flags.None;
            identifier = value.GetValueOrDefault();
        }
    }

    internal bool IsConfiguration
    {
        get => (flags & Flags.Configuration) is not 0;
        private init => flags |= value ? Flags.Configuration : Flags.None;
    }

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);
        writer += (byte)flags;
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(Length);
    }
    
    [Flags]
    private enum Flags : byte
    {
        None = 0,
        HasIdentifier = 1,
        Configuration = HasIdentifier << 1,
    }
}