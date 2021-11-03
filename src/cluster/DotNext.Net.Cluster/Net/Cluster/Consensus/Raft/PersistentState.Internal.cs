using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using static Threading.AtomicInt64;

public partial class PersistentState
{
    [Flags]
    private enum LogEntryFlags : uint
    {
        None = 0,

        HasIdentifier = 0x01,
    }

#pragma warning disable CA2252  // TODO: Remove in .NET 7
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct LogEntryMetadata : IBinaryFormattable<LogEntryMetadata>
    {
        internal const int Size = sizeof(LogEntryFlags) + sizeof(int) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);
        private readonly LogEntryFlags flags;
        private readonly int identifier;
        internal readonly long Term, Timestamp, Length, Offset;

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

        internal SnapshotMetadata(long index, DateTimeOffset timeStamp, long term, long length, int? id = null)
            : this(new LogEntryMetadata(timeStamp, term, Size, length, id), index)
        {
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
#pragma warning restore CA2252

    private sealed class VersionedFileReader : FileReader
    {
        private long version;

        internal VersionedFileReader(SafeFileHandle handle, int bufferSize, MemoryAllocator<byte> allocator, long version)
            : base(handle, bufferSize: bufferSize, allocator: allocator)
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
        private protected readonly FileWriter writer;
        private readonly MemoryAllocator<byte> allocator;
        internal readonly string FileName;

        // A pool of read-only readers that can be shared between multiple consumers in parallel.
        // The reader will be created on demand.
        private VersionedFileReader?[] readers;

        // This field is used to control 'freshness' of the read buffers
        private long version; // volatile

        private protected ConcurrentStorageAccess(string fileName, int bufferSize, MemoryAllocator<byte> allocator, int readersCount, FileOptions options, long initialSize)
        {
            Handle = File.Exists(fileName)
                ? File.OpenHandle(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, options)
                : File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, options, initialSize);

            writer = new(Handle, bufferSize: bufferSize, allocator: allocator);
            readers = new VersionedFileReader[readersCount];
            this.allocator = allocator;
            FileName = fileName;
            version = long.MinValue;

            if (readersCount == 1)
                readers[0] = new(Handle, bufferSize, allocator, version);
        }

        private protected long FileSize => RandomAccess.GetLength(Handle);

        internal void Invalidate() => version.IncrementAndGet();

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
        {
            Invalidate();
            return writer.WriteAsync(token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected FileReader GetSessionReader(int sessionId)
        {
            Debug.Assert(sessionId >= 0 && sessionId < readers.Length);

            var result = GetReader();

            if (result is null)
            {
                GetReader() = result = new(Handle, writer.MaxBufferSize, allocator, version.VolatileRead());
            }
            else
            {
                result.VerifyVersion(version.VolatileRead());
            }

            return result;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            ref VersionedFileReader? GetReader()
                => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(readers), sessionId);
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

                readers = Array.Empty<VersionedFileReader?>();
                writer.Dispose();
                Handle.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
