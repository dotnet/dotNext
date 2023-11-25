using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using Intrinsics = Runtime.Intrinsics;

public partial class PersistentState
{
    [Flags]
    private enum LogEntryFlags : uint
    {
        None = 0,

        HasIdentifier = 0x01,
    }

    // Perf: in case of LE, we want to store the metadata in the block of memory as-is
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LogEntryMetadata
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
            Term = reader.ReadLittleEndian<long>(isUnsigned: false);
            Timestamp = reader.ReadLittleEndian<long>(isUnsigned: false);
            Length = reader.ReadLittleEndian<long>(isUnsigned: false);
            Offset = reader.ReadLittleEndian<long>(isUnsigned: false);
            flags = (LogEntryFlags)reader.ReadLittleEndian<uint>(isUnsigned: true);
            identifier = reader.ReadLittleEndian<int>(isUnsigned: false);
        }

        internal LogEntryMetadata(ReadOnlySpan<byte> input)
        {
            Debug.Assert(Intrinsics.AlignOf<LogEntryMetadata>() is sizeof(long));
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

        internal int? Id => (flags & LogEntryFlags.HasIdentifier) != 0U ? identifier : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, long offset, long length)
            where TLogEntry : IRaftLogEntry
            => new(entry.Timestamp, entry.Term, offset, length, entry.CommandId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LogEntryMetadata Create(in CachedLogEntry entry, long offset)
            => new(entry.Timestamp, entry.Term, offset, entry.Length, entry.CommandId);

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
            else if (IntPtr.Size is sizeof(long))
            {
                // 64-bit LE case, the pointer is always aligned to 8 bytes
                Debug.Assert(Intrinsics.AddressOf(in ptr) % IntPtr.Size is 0);
                Unsafe.As<byte, LogEntryMetadata>(ref ptr) = this;
            }
            else
            {
                // 32-bit LE case, the pointer may not be aligned to 8 bytes
                Unsafe.WriteUnaligned(ref ptr, this);
            }
        }

        internal static long GetTerm(ReadOnlySpan<byte> input)
            => BinaryPrimitives.ReadInt64LittleEndian(input);

        private long End => Length + Offset;

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
                return reader.ReadLittleEndian<long>(isUnsigned: false) + reader.ReadLittleEndian<long>(isUnsigned: false); // Length + Offset
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct SnapshotMetadata
    {
        internal const int Size = sizeof(long) + LogEntryMetadata.Size;
        internal readonly long Index;
        internal readonly LogEntryMetadata RecordMetadata;

        private SnapshotMetadata(LogEntryMetadata metadata, long index)
        {
            Index = index;
            RecordMetadata = metadata;
        }

        internal SnapshotMetadata(ReadOnlySpan<byte> input)
        {
            Index = BinaryPrimitives.ReadInt64LittleEndian(input);
            RecordMetadata = new(input.Slice(sizeof(long)));
        }

        internal SnapshotMetadata(long index, DateTimeOffset timeStamp, long term, long length, int? id = null)
            : this(new LogEntryMetadata(timeStamp, term, Size, length, id), index)
        {
        }

        internal static SnapshotMetadata Create<TLogEntry>(TLogEntry snapshot, long index, long length)
            where TLogEntry : IRaftLogEntry
            => new(LogEntryMetadata.Create(snapshot, 0L, length), index);

        public void Format(Span<byte> output)
        {
            BinaryPrimitives.WriteInt64LittleEndian(output, Index);
            RecordMetadata.Format(output.Slice(sizeof(long)));
        }
    }

    private sealed class VersionedFileReader : FileReader
    {
        private long version;

        internal VersionedFileReader(SafeFileHandle handle, long fileOffset, int bufferSize, MemoryAllocator<byte> allocator, long version)
            : base(handle, fileOffset, bufferSize, allocator)
        {
            this.version = version;
        }

        internal void VerifyVersion(long expected)
        {
            if (version != expected)
                ClearBuffer();

            version = expected;
        }
    }

    internal abstract class ConcurrentStorageAccess : Disposable
    {
        internal readonly SafeFileHandle Handle;
        private readonly FileStream? streamForFlush;
        private protected readonly FileWriter writer;
        private protected readonly int fileOffset;
        private readonly MemoryAllocator<byte> allocator;
        internal readonly string FileName;

        // A pool of read-only readers that can be shared between multiple consumers in parallel.
        // The reader will be created on demand.
        private VersionedFileReader?[] readers;

        // This field is used to control 'freshness' of the read buffers
        private long version; // volatile

        private protected ConcurrentStorageAccess(string fileName, int fileOffset, int bufferSize, MemoryAllocator<byte> allocator, int readersCount, WriteMode writeMode, long initialSize)
        {
            var options = writeMode is WriteMode.WriteThrough
                ? FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.SequentialScan
                : FileOptions.Asynchronous | FileOptions.SequentialScan;

            FileMode fileMode;
            if (File.Exists(fileName))
            {
                fileMode = FileMode.OpenOrCreate;
                initialSize = 0L;
            }
            else
            {
                fileMode = FileMode.CreateNew;
                initialSize += fileOffset;
            }

            Handle = File.OpenHandle(fileName, fileMode, FileAccess.ReadWrite, FileShare.Read, options, initialSize);

            this.fileOffset = fileOffset;
            writer = new(Handle, fileOffset, bufferSize, allocator);
            readers = new VersionedFileReader[readersCount];
            this.allocator = allocator;
            FileName = fileName;
            version = long.MinValue;

            if (readersCount == 1)
                readers[0] = new(Handle, fileOffset, bufferSize, allocator, version);

            streamForFlush = writeMode is WriteMode.AutoFlush
                ? new(Handle, FileAccess.Write, bufferSize: 1)
                : null;
        }

        internal long FileSize => RandomAccess.GetLength(Handle);

        internal void Invalidate() => Interlocked.Increment(ref version);

        internal ValueTask SetWritePositionAsync(long position, CancellationToken token = default)
        {
            var result = ValueTask.CompletedTask;

            if (!writer.HasBufferedData)
            {
                writer.FilePosition = position;
            }
            else if (position != writer.FilePosition)
            {
                result = FlushAndSetPositionAsync(position, token);
            }

            return result;
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask FlushAndSetPositionAsync(long position, CancellationToken token)
        {
            await FlushAsync(token).ConfigureAwait(false);
            writer.FilePosition = position;
        }

        public virtual ValueTask FlushAsync(CancellationToken token = default)
        {
            Invalidate();
            return streamForFlush is null ? writer.WriteAsync(token) : FlushToDiskAsync(writer, streamForFlush, token);

            static async ValueTask FlushToDiskAsync(FileWriter writer, FileStream streamForFlush, CancellationToken token)
            {
                await writer.WriteAsync(token).ConfigureAwait(false);
                streamForFlush.Flush(flushToDisk: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected FileReader GetSessionReader(int sessionId)
        {
            Debug.Assert(sessionId >= 0 && sessionId < readers.Length);

            ref var result = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(readers), sessionId);

            var version = Volatile.Read(in this.version);
            if (result is null)
            {
                result = new(Handle, fileOffset, writer.MaxBufferSize, allocator, version);
            }
            else
            {
                result.VerifyVersion(version);
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (ref var reader in readers.AsSpan())
                {
                    var stream = reader;
                    reader = null;
                    stream?.Dispose();
                }

                readers = [];
                writer.Dispose();
                Handle.Dispose();
                streamForFlush?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Lazy list of log entries optimized in the following aspects:
    // 1. It doesn't depend on the actual number of log entries in the list
    // 2. It's optimized for sequential reads only, when indexer is accessed for
    //      monotonically increasing argument. In this case, indexer provides O(1) access time
    // These assumptions are valid when the list is consumed by typical Raft algorithm implementation.
    // Raft sequentially checks Term for each entry and then sequentially transmits entries one-by-one sequentially
    // over the wire.
    [StructLayout(LayoutKind.Auto)]
    private struct LogEntryList : IReadOnlyList<LogEntry>
    {
        private readonly PersistentState state;
        internal readonly long StartIndex, EndIndex;
        internal readonly int SessionId;
        private readonly bool metadataOnly;
        private readonly Partition? head; // partition containing the first log entry in the list
        private Partition? cache;
        internal IAsyncBinaryReader? Snapshot;

        internal LogEntryList(PersistentState state, int sessionId, long startIndex, long endIndex, int count, bool metadataOnly)
        {
            this.state = state;
            StartIndex = startIndex;
            EndIndex = endIndex;
            this.metadataOnly = metadataOnly;
            if(!state.TryGetPartition(startIndex, ref head))
                head = state.FirstPartition;

            cache = head;
            Count = count;
            SessionId = sessionId;
        }

        public readonly int Count { get; }

        private readonly LogEntry CreateSnapshotEntry()
            => new(in state.SnapshotInfo) { ContentReader = Snapshot };

        public LogEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var absoluteIndex = StartIndex;

                if (index is not 0)
                {
                    absoluteIndex += index;
                }
                else if (absoluteIndex is 0L)
                {
                    return LogEntry.Initial;
                }
                else if (absoluteIndex == state.SnapshotInfo.Index)
                {
                    return CreateSnapshotEntry();
                }
                else
                {
                    cache = head;
                }

                Debug.Assert(absoluteIndex <= EndIndex);

                return state.TryGetPartition(absoluteIndex, ref cache)
                    ? cache.Read(SessionId, absoluteIndex, metadataOnly)
                    : throw new MissingPartitionException(absoluteIndex);
            }
        }

        public readonly IEnumerator<LogEntry> GetEnumerator()
        {
            var runningIndex = StartIndex;

            if (runningIndex is 0L)
            {
                yield return LogEntry.Initial;
                runningIndex = 1L;
            }
            else if (runningIndex == state.SnapshotInfo.Index)
            {
                yield return CreateSnapshotEntry();
                runningIndex += 1L;
            }

            for (Partition? partition = head; runningIndex <= EndIndex && state.TryGetPartition(runningIndex, ref partition); runningIndex++)
                yield return partition.Read(SessionId, runningIndex, metadataOnly);
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}