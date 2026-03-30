using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(Flags) + sizeof(int);

    internal readonly long Term;
    private readonly long length;
    private readonly Flags flags;
    private readonly int identifier;

    private LogEntryMetadata(long term, long? length)
    {
        Term = term;
        this.length = length.GetValueOrDefault(-1L);
    }

    internal static LogEntryMetadata Create<TEntry>(TEntry entry) where TEntry : IRaftLogEntry => new(entry.Term, entry.Length)
    {
        CommandId = entry.CommandId,
        IsSnapshot = entry.IsSnapshot,
        IsConfiguration = entry.IsConfiguration,
    };

    private LogEntryMetadata(ref SpanReader<byte> reader)
    {
        Term = reader.ReadLittleEndian<long>();
        flags = (Flags)reader.Read();
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

    internal long? Length => length >= 0L ? length : null;

    internal int? CommandId
    {
        get => (flags & Flags.HasIdentifier) is not 0 ? identifier : null;
        private init
        {
            flags |= value.HasValue ? Flags.HasIdentifier : Flags.None;
            identifier = value.GetValueOrDefault();
        }
    }

    internal bool IsSnapshot
    {
        get => (flags & Flags.Snapshot) is not 0;
        private init => flags |= value ? Flags.Snapshot : Flags.None;
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
        writer.Add((byte)flags);
        writer.WriteLittleEndian(identifier);
        writer.WriteLittleEndian(length);
    }
    
    [Flags]
    private enum Flags : byte
    {
        None = 0,
        HasIdentifier = 1,
        Snapshot = HasIdentifier << 1,
        Configuration = Snapshot << 1,
    }
}