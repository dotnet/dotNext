using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;

public partial class PersistentState
{
    [Flags]
    private enum LogEntryFlags : uint
    {
        None = 0,

        HasIdentifier = 0x01,
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
    {
        internal const int Size = sizeof(LogEntryFlags) + sizeof(int) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);
        private readonly LogEntryFlags flags;
        private readonly int identifier;
        internal readonly long Term, Timestamp, Length, Offset;

        private LogEntryMetadata(DateTimeOffset timeStamp, long term, long offset, long length, int? id)
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

        internal LogEntryMetadata(ref SpanReader<byte> reader)
        {
            Term = reader.ReadInt64(true);
            Timestamp = reader.ReadInt64(true);
            Length = reader.ReadInt64(true);
            Offset = reader.ReadInt64(true);
            flags = (LogEntryFlags)reader.ReadUInt32(true);
            identifier = reader.ReadInt32(true);
        }

        static int IBinaryFormattable<LogEntryMetadata>.Size => Size;

        static LogEntryMetadata IBinaryFormattable<LogEntryMetadata>.Parse(ref SpanReader<byte> input)
            => new(ref input);

        internal int? Id => (flags & LogEntryFlags.HasIdentifier) != 0U ? identifier : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, long offset, long length)
            where TLogEntry : IRaftLogEntry
            => new(entry.Timestamp, entry.Term, offset, length, entry.CommandId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LogEntryMetadata Create(in CachedLogEntry entry, long offset)
            => new(entry.Timestamp, entry.Term, offset, entry.Length, entry.CommandId);

        public void Format(ref SpanWriter<byte> writer)
        {
            writer.WriteInt64(Term, true);
            writer.WriteInt64(Timestamp, true);
            writer.WriteInt64(Length, true);
            writer.WriteInt64(Offset, true);
            writer.WriteUInt32((uint)flags, true);
            writer.WriteInt32(identifier, true);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct SnapshotMetadata : IBinaryFormattable<SnapshotMetadata>
    {
        internal const int Size = sizeof(long) + LogEntryMetadata.Size;
        internal readonly long Index;
        internal readonly LogEntryMetadata RecordMetadata;

        private SnapshotMetadata(LogEntryMetadata metadata, long index)
        {
            Index = index;
            RecordMetadata = metadata;
        }

        internal SnapshotMetadata(ref SpanReader<byte> reader)
        {
            Index = reader.ReadInt64(true);
            RecordMetadata = new(ref reader);
        }

        static int IBinaryFormattable<SnapshotMetadata>.Size => Size;

        static SnapshotMetadata IBinaryFormattable<SnapshotMetadata>.Parse(ref SpanReader<byte> input)
            => new(ref input);

        internal static SnapshotMetadata Create<TLogEntry>(TLogEntry snapshot, long index, long length)
            where TLogEntry : IRaftLogEntry
            => new(LogEntryMetadata.Create(snapshot, Size, length), index);

        public void Format(ref SpanWriter<byte> writer)
        {
            writer.WriteInt64(Index, true);
            RecordMetadata.Format(ref writer);
        }
    }

    private abstract class ConcurrentStorageAccess : Disposable
    {
        private readonly SafeFileHandle handle;
        private protected readonly FileWriter writer;
        private readonly MemoryAllocator<byte> allocator;
        internal readonly string FileName;

        // A pool of read-only readers that can be shared between multiple consumers in parallel.
        // The reader will be created on demand.
        private FileReader?[] readers;

        private protected ConcurrentStorageAccess(string fileName, int bufferSize, MemoryAllocator<byte> allocator, int readersCount, FileOptions options, long initialSize)
        {
            handle = File.OpenHandle(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, options, initialSize);
            writer = new(handle, bufferSize: bufferSize, allocator: allocator);
            readers = new FileReader[readersCount];
            this.allocator = allocator;
            FileName = fileName;

            if (readersCount == 1)
                readers[0] = new(handle, bufferSize: bufferSize, allocator: allocator);
        }

        private protected long FileSize => RandomAccess.GetLength(handle);

        /*
         * This method allows to reset read cache. It's an expensive operation and we
         * actually need this in two cases: when dropping log entries and when rewriting uncommitted entries
         */
        private protected void InvalidateReaders()
        {
            foreach (var reader in readers)
            {
                reader?.ClearBuffer();
            }
        }

        internal ValueTask SetWritePositionAsync(long value, CancellationToken token = default)
        {
            var result = ValueTask.CompletedTask;

            if (!writer.HasBufferedData)
            {
                writer.FilePosition = value;
            }
            else if (value != writer.FilePosition)
            {
                result = FlushAndSetPositionAsync(value, token);
            }

            return result;

            async ValueTask FlushAndSetPositionAsync(long value, CancellationToken token)
            {
                await FlushAsync(token).ConfigureAwait(false);
                writer.FilePosition = value;
            }
        }

        internal abstract ValueTask WriteAsync<TEntry>(TEntry entry, long index, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry;

        public virtual ValueTask FlushAsync(CancellationToken token = default)
            => writer.WriteAsync(token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected FileReader GetSessionReader(int sessionId)
        {
            Debug.Assert(sessionId >= 0 && sessionId < readers.Length);

            ref var reader = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(readers), sessionId);
            return reader ??= new(handle, bufferSize: writer.MaxBufferSize, allocator: allocator);
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

                readers = Array.Empty<FileReader?>();
                writer.Dispose();
                handle.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
