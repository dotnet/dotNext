using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Buffers.Binary;
using Runtime;

[StructLayout(LayoutKind.Sequential)] // Perf: in case of LE, we want to store the metadata in the block of memory as-is
internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
{
    internal const int AlignedSize = 64; // a multiple of the system page size
    private const int Size = sizeof(LogEntryFlags) + sizeof(int) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);

    internal readonly long Term;
    internal readonly long Timestamp;
    internal readonly long Length;
    internal readonly ulong Offset;
    private readonly LogEntryFlags flags;
    private readonly int identifier;

    internal LogEntryMetadata(DateTimeOffset timeStamp, long term, ulong offset, long length, int? id = null)
    {
        Debug.Assert(AlignedSize >= Size);

        Term = term;
        Timestamp = timeStamp.UtcTicks;
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
        Timestamp = reader.ReadLittleEndian<long>();
        Length = reader.ReadLittleEndian<long>();
        Offset = reader.ReadLittleEndian<ulong>();
        flags = (LogEntryFlags)reader.ReadLittleEndian<uint>();
        identifier = reader.ReadLittleEndian<int>();
    }

    internal LogEntryMetadata(ReadOnlySpan<byte> input)
    {
        Debug.Assert(AlignedSize >= Size);

        Debug.Assert(Intrinsics.AlignOf<LogEntryMetadata>() is sizeof(long));
        Debug.Assert(Size % sizeof(long) is 0);
        Debug.Assert(input.Length >= Size);

        // fast path without any overhead for LE byte order
        ref var ptr = ref MemoryMarshal.GetReference(input);

        if (!BitConverter.IsLittleEndian)
        {
            // BE case
            Create(input, out this);
        }
        else if (IntPtr.Size is sizeof(long))
        {
            // 64-bit LE case, the pointer is always aligned to 8 bytes
            Debug.Assert(Intrinsics.AddressOf(in ptr) % IntPtr.Size is 0);
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

    static int IBinaryFormattable<LogEntryMetadata>.Size => Size;

    internal int? Id => HasIdentifier ? identifier : null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool HasIdentifier => (flags & LogEntryFlags.HasIdentifier) is not 0U;

    internal bool HasPayload => Length > 0L || HasIdentifier;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, ulong offset, long length)
        where TLogEntry : IRaftLogEntry
        => new(entry.Timestamp, entry.Term, offset, length, entry.CommandId);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FormatSlow(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Term);
        writer.WriteLittleEndian(Timestamp);
        writer.WriteLittleEndian(Length);
        writer.WriteLittleEndian(Offset);
        writer.WriteLittleEndian((uint)flags);
        writer.WriteLittleEndian(identifier);
    }

    public void Format(Span<byte> output)
    {
        Debug.Assert(Intrinsics.AlignOf<LogEntryMetadata>() is sizeof(long));
        Debug.Assert(output.Length >= Size);

        // fast path without any overhead for LE byte order
        ref var ptr = ref MemoryMarshal.GetReference(output);

        if (!BitConverter.IsLittleEndian)
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