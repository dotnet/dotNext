using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Buffers;
using DotNext.Buffers.Binary;
using DotNext.Runtime;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public partial class PersistentState
{
    [Flags]
    private enum LogEntryFlags : uint
    {
        /// <summary>
        /// Metadata record is not initialized.
        /// </summary>
        Uninitialized = 0,
        
        /// <summary>
        /// Means that the metadata record is initialized
        /// </summary>
        Initialized = 0x01,

        HasIdentifier = 0x02,
    }

    // Perf: in case of LE, we want to store the metadata in the block of memory as-is
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
    {
        internal const int Size = sizeof(LogEntryFlags) + sizeof(int) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);

        internal readonly long Term;
        internal readonly long Timestamp;
        internal readonly long Length;
        internal readonly long Offset;
        private readonly LogEntryFlags flags;
        private readonly int identifier;

        internal LogEntryMetadata(DateTimeOffset timeStamp, long term, long offset, long length, int? id = null)
        {
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
            Term = reader.ReadLittleEndian<long>();
            Timestamp = reader.ReadLittleEndian<long>();
            Length = reader.ReadLittleEndian<long>();
            Offset = reader.ReadLittleEndian<long>();
            flags = (LogEntryFlags)reader.ReadLittleEndian<uint>();
            identifier = reader.ReadLittleEndian<int>();
        }

        internal LogEntryMetadata(ReadOnlySpan<byte> input)
        {
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

        internal int? Id => (flags & LogEntryFlags.HasIdentifier) is not 0U ? identifier : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, long offset, long length)
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

            if (BitConverter.IsLittleEndian)
            {
                // LE case, the pointer may not be aligned to 8 bytes
                Unsafe.WriteUnaligned(ref ptr, this);
            }
            else
            {
                // BE case
                FormatSlow(output);
            }
        }

        internal long End => Length + Offset;

        internal static long GetEndOfLogEntry(ReadOnlySpan<byte> input)
        {
            Debug.Assert(Intrinsics.AlignOf<LogEntryMetadata>() is sizeof(long));
            ref var ptr = ref MemoryMarshal.GetReference(input);

            // BE case
            if (!BitConverter.IsLittleEndian)
                return GetEndOfLogEntrySlow(input);

            // 64-bit LE case, the pointer is always aligned to 8 bytes
            if (IntPtr.Size is sizeof(long))
            {
                Debug.Assert(Intrinsics.AddressOf(in ptr) % IntPtr.Size is 0);
                return Unsafe.As<byte, LogEntryMetadata>(ref ptr).End;
            }

            // 32-bit LE case, the pointer may not be aligned to 8 bytes
            return Unsafe.ReadUnaligned<LogEntryMetadata>(ref ptr).End;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static long GetEndOfLogEntrySlow(ReadOnlySpan<byte> input)
            {
                var reader = new SpanReader<byte>(input);
                reader.Advance(sizeof(long) + sizeof(long)); // skip Term and Timestamp
                return reader.ReadLittleEndian<long>() + reader.ReadLittleEndian<long>(); // Length + Offset
            }
        }
    }
}