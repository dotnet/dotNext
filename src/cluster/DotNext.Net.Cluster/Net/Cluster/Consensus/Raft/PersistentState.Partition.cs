using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using static Runtime.InteropServices.SafeBufferExtensions;
using IntegrityException = IO.Log.IntegrityException;
using LogEntryReadOptimizationHint = IO.Log.LogEntryReadOptimizationHint;

public partial class PersistentState
{
    /*
        Partition file format:
        FileName - number of partition
        Payload:
        [octet string] X number of entries

        FileName.meta - metadata table for the partition
        Payload:
        [struct LogEntryMetadata] X Capacity
     */
    private sealed class Partition : ConcurrentStorageAccess
    {
        internal const int MaxRecordsPerPartition = int.MaxValue / LogEntryMetadata.Size;
        private static readonly MemoryOwner<byte> EmptyBuffer;

        internal readonly long FirstIndex, PartitionNumber, LastIndex;
        private MemoryOwner<MemoryOwner<byte>> entryCache;
        private Partition? previous, next;

        // metadata management
        private MemoryOwner<byte> metadata;
        private int metadataFlushStartAddress;
        private int metadataFlushEndAddress;

        internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, bool writeThrough, long initialSize)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), checked(LogEntryMetadata.Size * recordsPerPartition), bufferSize, manager.BufferAllocator, readersCount, GetOptions(writeThrough), initialSize)
        {
            FirstIndex = partitionNumber * recordsPerPartition;
            LastIndex = FirstIndex + recordsPerPartition - 1L;
            PartitionNumber = partitionNumber;

            // allocate metadata segment
            metadata = manager.BufferAllocator.Invoke(fileOffset, true);
            metadataFlushStartAddress = int.MaxValue;

            entryCache = manager.AllocLogEntryCache(recordsPerPartition);
        }

        private async Task InitializeAsync()
        {
            if (await RandomAccess.ReadAsync(Handle, metadata.Memory, 0L).ConfigureAwait(false) < fileOffset)
                throw new CorruptedPartitionException();
        }

        internal void Initialize()
        {
            using var task = InitializeAsync();
            task.Wait();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileOptions GetOptions(bool writeThrough)
        {
            const FileOptions skipBufferOptions = FileOptions.WriteThrough | FileOptions.Asynchronous;
            const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous;
            return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ToRelativeIndex(long absoluteIndex)
            => unchecked((int)(absoluteIndex - FirstIndex));

        internal bool IsHead => previous is null;

        internal bool IsTail => next is null;

        internal Partition? Next => next;

        internal Partition? Previous => previous;

        internal void Append(Partition partition)
        {
            Debug.Assert(PartitionNumber < partition.PartitionNumber);
            partition.previous = this;
            partition.next = next;
            if (next is not null)
                next.previous = partition;
            next = partition;
        }

        internal void Prepend(Partition partition)
        {
            Debug.Assert(PartitionNumber > partition.PartitionNumber);
            partition.previous = previous;
            partition.next = this;
            if (previous is not null)
                previous.next = partition;
            previous = partition;
        }

        internal void Detach()
        {
            if (previous is not null)
                previous.next = next;
            if (next is not null)
                next.previous = previous;

            next = previous = null;
        }

        internal void DetachAncestor()
        {
            if (previous is not null)
                previous.next = null;
            previous = null;
        }

        internal bool Contains(long recordIndex)
            => recordIndex >= FirstIndex && recordIndex <= LastIndex;

        private async ValueTask FlushAsync(ReadOnlyMemory<byte> metadata, CancellationToken token)
        {
            await RandomAccess.WriteAsync(Handle, metadata, metadataFlushStartAddress, token).ConfigureAwait(false);
            metadataFlushStartAddress = int.MaxValue;
            metadataFlushEndAddress = 0;

            await base.FlushAsync(token).ConfigureAwait(false);
        }

        public override ValueTask FlushAsync(CancellationToken token = default)
        {
            var size = metadataFlushEndAddress - metadataFlushStartAddress;
            return size > 0
                ? FlushAsync(metadata.Memory.Slice(metadataFlushStartAddress, size), token)
                : base.FlushAsync(token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref byte GetMetadata(int index, out int offset)
        {
            Debug.Assert(metadata.Length == fileOffset);

            return ref Unsafe.Add(ref BufferHelpers.GetReference(in metadata), offset = index * LogEntryMetadata.Size);
        }

        private unsafe T ReadMetadata<T>(int index, delegate*<ref SpanReader<byte>, T> parser)
            where T : unmanaged
        {
            Debug.Assert(parser != null);

            var reader = new SpanReader<byte>(ref GetMetadata(index, out _), LogEntryMetadata.Size);
            return parser(ref reader);
        }

        private void WriteMetadata(int index, in LogEntryMetadata metadata)
        {
            var writer = new SpanWriter<byte>(ref GetMetadata(index, out var offset), LogEntryMetadata.Size);
            metadata.Format(ref writer);

            metadataFlushStartAddress = Math.Min(metadataFlushStartAddress, offset);
            metadataFlushEndAddress = Math.Max(metadataFlushEndAddress, offset + LogEntryMetadata.Size);
        }

        internal unsafe long GetTerm(long absoluteIndex)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            return ReadMetadata(ToRelativeIndex(absoluteIndex), &LogEntryMetadata.GetTerm);
        }

        internal unsafe LogEntry Read(int sessionId, long absoluteIndex, LogEntryReadOptimizationHint hint = LogEntryReadOptimizationHint.None)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            var relativeIndex = ToRelativeIndex(absoluteIndex);
            var metadata = ReadMetadata(relativeIndex, &LogEntryMetadata.Parse);

            ref readonly var cachedContent = ref EmptyBuffer;

            if (hint == LogEntryReadOptimizationHint.MetadataOnly)
                goto return_cached;

            if (!entryCache.IsEmpty)
                cachedContent = ref entryCache[relativeIndex];

            if (cachedContent.IsEmpty && metadata.Length > 0L)
                return new(GetSessionReader(sessionId), in metadata, absoluteIndex);

        return_cached:
            return new(in cachedContent, in metadata, absoluteIndex);
        }

        private void UpdateCache(in CachedLogEntry entry, int index, long offset)
        {
            Debug.Assert(entryCache.IsEmpty is false);
            Debug.Assert(index >= 0 && index < entryCache.Length);

            ref var cacheEntry = ref entryCache[index];
            cacheEntry.Dispose();
            cacheEntry = entry.Content;

            // save new log entry to the allocation table
            WriteMetadata(index, LogEntryMetadata.Create(in entry, offset));
        }

        internal ValueTask PersistCachedEntryAsync(long absoluteIndex, long offset, bool removeFromMemory)
        {
            Debug.Assert(entryCache.IsEmpty is false);

            var index = ToRelativeIndex(absoluteIndex);
            Debug.Assert(index >= 0 && index < entryCache.Length);

            ReadOnlyMemory<byte> content = entryCache[index].Memory;

            return content.IsEmpty
                ? ValueTask.CompletedTask
                : removeFromMemory
                ? PersistAndDeleteAsync()
                : PersistAsync();

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
            async ValueTask PersistAsync()
            {
                await SetWritePositionAsync(offset).ConfigureAwait(false);
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
            async ValueTask PersistAndDeleteAsync()
            {
                try
                {
                    await PersistAsync().ConfigureAwait(false);
                }
                finally
                {
                    entryCache[index].Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFirstEntry(int index)
            => index == 0 || index == 1 && FirstIndex == 0L;

        private async ValueTask WriteAsync<TEntry>(TEntry entry, int index, long offset, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            // slow path - persist log entry
            await SetWritePositionAsync(offset, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);

            // save new log entry to the allocation table
            WriteMetadata(index, LogEntryMetadata.Create(entry, offset, writer.WritePosition - offset));
        }

        internal override unsafe ValueTask WriteAsync<TEntry>(TEntry entry, long absoluteIndex, CancellationToken token = default)
        {
            // write operation always expects absolute index so we need to convert it to the relative index
            var relativeIndex = ToRelativeIndex(absoluteIndex);
            Debug.Assert(absoluteIndex >= FirstIndex && relativeIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            // calculate offset of the previous entry
            var offset = IsFirstEntry(relativeIndex)
                ? fileOffset
                : ReadMetadata(relativeIndex - 1, &LogEntryMetadata.GetEndOfLogEntry);

            if (typeof(TEntry) == typeof(CachedLogEntry))
            {
                // fast path - just add cached log entry to the cache table
                UpdateCache(in Unsafe.As<TEntry, CachedLogEntry>(ref entry), relativeIndex, offset);
                return ValueTask.CompletedTask;
            }

            return WriteAsync(entry, relativeIndex, offset, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                metadata.Dispose();
                entryCache.ReleaseAll();
                previous = next = null;
            }

            base.Dispose(disposing);
        }
    }

    /*
     * Binary format:
     * [struct SnapshotMetadata] X 1
     * [octet string] X 1
     */
    internal sealed class Snapshot : ConcurrentStorageAccess
    {
        private new const string FileName = "snapshot";
        private const string TempFileName = "snapshot.new";

        private MemoryOwner<byte> metadataBuffer;
        private SnapshotMetadata metadata;

        internal Snapshot(DirectoryInfo location, int bufferSize, in BufferManager manager, int readersCount, bool writeThrough, bool tempSnapshot = false, long initialSize = 0L)
            : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), SnapshotMetadata.Size, bufferSize, manager.BufferAllocator, readersCount, GetOptions(writeThrough), initialSize)
        {
            metadataBuffer = manager.BufferAllocator.Invoke(SnapshotMetadata.Size, true);
        }

        // cache flag that allows to avoid expensive access to Length that can cause native call
        internal bool IsEmpty => metadata.Index == 0L;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileOptions GetOptions(bool writeThrough)
        {
            const FileOptions skipBufferOptions = FileOptions.Asynchronous | FileOptions.WriteThrough;
            const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous;
            return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
        }

        internal void Initialize()
        {
            using var task = InitializeAsync();
            task.Wait();
        }

        private async Task InitializeCoreAsync()
        {
            var memory = metadataBuffer.Memory;
            if (await RandomAccess.ReadAsync(Handle, memory, 0L).ConfigureAwait(false) >= fileOffset)
#pragma warning disable CA2252  // TODO: Remove in .NET 7
                metadata = IBinaryFormattable<SnapshotMetadata>.Parse(memory.Span);
#pragma warning restore CA2252
            else
                throw new CorruptedPartitionException();
        }

        internal Task InitializeAsync()
            => FileSize >= fileOffset ? InitializeCoreAsync() : Task.CompletedTask;

        private ReadOnlyMemory<byte> SerializeMetadata()
        {
            var result = metadataBuffer.Memory;
            var writer = new SpanWriter<byte>(result.Span);
            metadata.Format(ref writer);
            return result;
        }

        public override async ValueTask FlushAsync(CancellationToken token = default)
        {
            await RandomAccess.WriteAsync(Handle, SerializeMetadata(), 0L, token).ConfigureAwait(false);
            await base.FlushAsync(token).ConfigureAwait(false);
        }

        internal ValueTask WriteMetadataAsync(long index, DateTimeOffset timestamp, long term, CancellationToken token = default)
        {
            metadata = new(index, timestamp, term, FileSize - fileOffset);
            return RandomAccess.WriteAsync(Handle, SerializeMetadata(), 0L, token);
        }

        internal override async ValueTask WriteAsync<TEntry>(TEntry entry, long index, CancellationToken token = default)
        {
            // write snapshot
            await SetWritePositionAsync(fileOffset, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);

            // update metadata
            metadata = SnapshotMetadata.Create(entry, index, writer.WritePosition - fileOffset);
        }

        // optimization hint is not supported for snapshots
        internal LogEntry Read(int sessionId)
            => new LogEntry(GetSessionReader(sessionId), in metadata);

        // cached index of the snapshotted entry
        internal ref readonly SnapshotMetadata Metadata => ref metadata;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                metadataBuffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Indicates that the log entry doesn't have a partition.
    /// </summary>
    public sealed class MissingPartitionException : IntegrityException
    {
        internal MissingPartitionException(long index)
            : base(ExceptionMessages.MissingPartition(index))
            => Index = index;

        /// <summary>
        /// Gets the index of the log entry.
        /// </summary>
        public long Index { get; }
    }

    /// <summary>
    /// Indicates that the partition containing log entries is corrupted.
    /// </summary>
    public sealed class CorruptedPartitionException : IntegrityException
    {
        internal CorruptedPartitionException()
            : base(ExceptionMessages.CorruptedPartition)
        {
        }
    }

    private readonly int recordsPerPartition;

    // Maintaining efficient data structure for a collection of partitions with the following characteristics:
    // 1. Committed partitions must be removed from the head of the list
    // 2. Uncommitted partitions must be removed from the tail of the list
    // 2. New partitions must be added to the tail of the list
    // 3. The list is sorted in ascending order (head is a partition with smaller number, tail is a partition with higher number)
    // 4. The thread that is responsible for removing partitions from the head (compaction thread) doesn't have
    // concurrency with the thread that is adding new partitions
    // Under the hood, this is simply a sorted linked list
    [SuppressMessage("Usage", "CA2213", Justification = "Disposed as a part of the linked list")]
    private Partition? head, tail;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

    private bool HasPartitions => tail is not null;

    private partial Partition CreatePartition(long partitionNumber);

    // during insertion the index is growing monothonically so
    // this method is optimized for forward lookup in sorted list of partitions
    private void GetOrCreatePartition(long recordIndex, [NotNull] ref Partition? partition)
    {
        var partitionNumber = PartitionOf(recordIndex);

        if (tail is null)
        {
            Debug.Assert(head is null);
            Debug.Assert(partition is null);
            head = tail = partition = CreatePartition(partitionNumber);
            goto exit;
        }

        Debug.Assert(head is not null);
        partition ??= tail;

        for (int previous = 0, current; ; previous = current)
        {
            switch (current = partitionNumber.CompareTo(partition.PartitionNumber))
            {
                case > 0:
                    if (previous < 0)
                    {
                        partition = Append(partitionNumber, partition);
                        goto exit;
                    }

                    // nothing on the right side, create new tail
                    if (partition.IsTail)
                    {
                        tail = partition = Append(partitionNumber, partition);
                        goto exit;
                    }

                    partition = partition.Next;
                    break;
                case < 0:
                    if (previous > 0)
                    {
                        partition = Prepend(partitionNumber, partition);
                        goto exit;
                    }

                    // nothing on the left side, create new head
                    if (partition.IsHead)
                    {
                        head = partition = Prepend(partitionNumber, partition);
                        goto exit;
                    }

                    partition = partition.Previous;
                    break;
                default:
                    goto exit;
            }

            Debug.Assert(partition is not null);
        }

    exit:
        return;

        Partition Prepend(long partitionNumber, Partition partition)
        {
            var tmp = CreatePartition(partitionNumber);
            partition.Prepend(tmp);
            return tmp;
        }

        Partition Append(long partitionNumber, Partition partition)
        {
            var tmp = CreatePartition(partitionNumber);
            partition.Append(tmp);
            return tmp;
        }
    }

    private Partition? TryGetPartition(long partitionNumber)
    {
        Partition? result = tail;
        if (result is null)
            goto exit;

        for (int previous = 0, current; ; previous = current)
        {
            switch (current = partitionNumber.CompareTo(result.PartitionNumber))
            {
                case > 0:
                    if (previous < 0 || result.IsTail)
                    {
                        result = null;
                        goto exit;
                    }

                    result = result.Next;
                    break;
                case < 0:
                    if (previous > 0 || result.IsHead)
                    {
                        result = null;
                        goto exit;
                    }

                    result = result.Previous;
                    break;
                default:
                    goto exit;
            }

            Debug.Assert(result is not null);
        }

    exit:
        return result;
    }

    // during reads the index is growing monothonically
    private bool TryGetPartition(long recordIndex, [NotNullWhen(true)] ref Partition? partition)
    {
        if (partition is not null && partition.Contains(recordIndex))
            goto success;

        if (tail is null)
        {
            Debug.Assert(head is null);
            Debug.Assert(partition is null);
            goto fail;
        }

        Debug.Assert(head is not null);
        partition ??= tail;

        var partitionNumber = PartitionOf(recordIndex);

        for (int previous = 0, current; ; previous = current)
        {
            switch (current = partitionNumber.CompareTo(partition.PartitionNumber))
            {
                case > 0:
                    if (previous < 0 || partition.IsTail)
                        goto fail;

                    partition = partition.Next;
                    break;
                case < 0:
                    if (previous > 0 || partition.IsHead)
                        goto fail;

                    partition = partition.Previous;
                    break;
                default:
                    goto success;
            }

            Debug.Assert(partition is not null);
        }

    success:
        return true;

    fail:
        return false;
    }

    private static void DeletePartition(Partition partition)
    {
        var fileName = partition.FileName;
        partition.Dispose();
        File.Delete(fileName);
    }

    // this method should be called for detached partition head only
    private static void DeletePartitions(Partition? current)
    {
        for (Partition? next; current is not null; current = next)
        {
            next = current.Next;
            DeletePartition(current);
        }
    }

    private Partition? DetachPartitions(long upperBoundIndex)
    {
        Partition? result = head, current;
        for (current = result; current is not null && current.LastIndex <= upperBoundIndex; current = current.Next);

        if (current is null)
        {
            head = tail = null;
        }
        else
        {
            current.DetachAncestor();
            head = current;
        }

        return result;
    }

    private void InvalidatePartitions(long upToIndex)
    {
        for (Partition? partition = tail; partition is not null && partition.LastIndex >= upToIndex; partition = partition.Previous)
            partition.Invalidate();
    }
}
