using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Buffers.Binary;
using Numerics;
using Runtime.CompilerServices;

[StructLayout(LayoutKind.Sequential)] // Perf: in case of LE, we want to store the metadata in the block of memory as-is
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    public readonly long Term;
    public readonly long Length;
    public readonly ulong Offset;
    private readonly LogEntryFlags flags;
    private readonly int identifier;

    internal LogEntryMetadata(long term, ulong offset, long length, int? id = null)
    {
        Debug.Assert(AlignedSize >= Size);

        Term = term;
        Length = length;
        Offset = offset;
        flags = LogEntryFlags.None;
        if (id.HasValue)
            flags |= LogEntryFlags.HasIdentifier;
        identifier = id.GetValueOrDefault();
    }

    // slow version if target architecture has BE byte order or pointer is not aligned
    private LogEntryMetadata(ref SpanReader<byte> reader)
    {
        Debug.Assert(AlignedSize >= Size);

        Term = reader.ReadLittleEndian<long>();

        if (!Features.IsTimestampIgnored)
            reader.ReadLittleEndian<long>();
        
        Length = reader.ReadLittleEndian<long>();
        Offset = reader.ReadLittleEndian<ulong>();
        flags = reader.ReadLittleEndian<Enum<LogEntryFlags>>();
        identifier = reader.ReadLittleEndian<int>();
    }

    internal LogEntryMetadata(ReadOnlySpan<byte> input)
    {
        Debug.Assert(AlignedSize >= Size);

        Debug.Assert(LogEntryMetadata.Alignment is sizeof(long));
        Debug.Assert(Size % sizeof(long) is 0);
        Debug.Assert(input.Length >= Size);

        // fast path without any overhead for LE byte order
        ref var ptr = ref MemoryMarshal.GetReference(input);

        if (!BitConverter.IsLittleEndian || !Features.IsTimestampIgnored)
        {
            // BE case
            Create(input, out this);
        }
        else if (IntPtr.Size is sizeof(long))
        {
            // 64-bit LE case, the pointer is always aligned to 8 bytes
            Debug.Assert(AdvancedHelpers.AddressOf(in ptr) % IntPtr.Size is 0);
            this = Unsafe.As<byte, LogEntryMetadata>(ref ptr);
        }
        else
        {
            // 32-bit LE case, the pointer may not be aligned to 8 bytes
            this = Unsafe.ReadUnaligned<LogEntryMetadata>(ref ptr);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Create(ReadOnlySpan<byte> input, out LogEntryMetadata metadata)
        {
            var reader = new SpanReader<byte>(input);
            metadata = new(ref reader);
        }
    }

    static LogEntryMetadata IBinaryFormattable<LogEntryMetadata>.Parse(ReadOnlySpan<byte> input) => new(input);

    public static int Size
    {
        get
        {
            const int sizeWithoutTimestamp = sizeof(LogEntryFlags) // flags
                                             + sizeof(int) // identifier
                                             + sizeof(long) // term
                                             + sizeof(long) // length
                                             + sizeof(ulong); // offset

            return Features.IsTimestampIgnored
                ? sizeWithoutTimestamp
                : sizeWithoutTimestamp + sizeof(long); // + timestamp
        }
    }

    public static int AlignedSize => Features.IsTimestampIgnored ? 32 : 64; // a multiple of the system page size

    internal int? Id => HasIdentifier ? identifier : null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool HasIdentifier => (flags & LogEntryFlags.HasIdentifier) is not 0U;

    internal bool HasPayload => Length > 0L || HasIdentifier;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, ulong offset, long length)
        where TLogEntry : IRaftLogEntry
        => new(entry.Term, offset, length, entry.CommandId);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FormatSlow(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);

        if (!Features.IsTimestampIgnored)
            writer.WriteLittleEndian(0L);
        
        writer.WriteLittleEndian(Length);
        writer.WriteLittleEndian(Offset);
        writer.WriteLittleEndian<Enum<LogEntryFlags>>(new(flags));
        writer.WriteLittleEndian(identifier);
    }

    public void Format(Span<byte> output)
    {
        Debug.Assert(LogEntryMetadata.Alignment is sizeof(long));
        Debug.Assert(output.Length >= Size);

        // fast path without any overhead for LE byte order
        ref var ptr = ref MemoryMarshal.GetReference(output);

        if (!BitConverter.IsLittleEndian || !Features.IsTimestampIgnored)
        {
            // BE case
            FormatSlow(output);
        }
        else if (IntPtr.Size is sizeof(ulong))
        {
            Unsafe.As<byte, LogEntryMetadata>(ref ptr) = this;
        }
        else
        {
            // LE case, the pointer may not be aligned to 8 bytes
            Unsafe.WriteUnaligned(ref ptr, this);
        }
    }

    internal ulong End => (ulong)Length + Offset;

    [Flags]
    private enum LogEntryFlags : uint
    {
        None = 0,

        HasIdentifier = 0x01,
    }
}

file static class Features
{
    private const string TimestampIgnoreFeature = "DotNext.IO.WriteAheadLog.IgnoreTimestamp";

    [FeatureSwitchDefinition(TimestampIgnoreFeature)]
    public static bool IsTimestampIgnored { get; } = AppContext.IsFeatureSupported(TimestampIgnoreFeature);
}