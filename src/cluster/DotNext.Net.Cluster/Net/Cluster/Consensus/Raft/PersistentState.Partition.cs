using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IntegrityException = IO.Log.IntegrityException;

public partial class PersistentState
{
    private protected abstract class PartitionBase : ConcurrentStorageAccess
    {
        internal const int MaxRecordsPerPartition = int.MaxValue / LogEntryMetadata.Size;
        protected static readonly CacheRecord EmptyRecord = new();

        internal readonly long FirstIndex, PartitionNumber, LastIndex;
        private PartitionBase? previous, next;
        protected MemoryOwner<CacheRecord> entryCache;

        protected PartitionBase(DirectoryInfo location, int offset, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), offset, bufferSize, manager.BufferAllocator, readersCount, writeMode, initialSize)
        {
            FirstIndex = partitionNumber * recordsPerPartition;
            LastIndex = FirstIndex + recordsPerPartition - 1L;
            PartitionNumber = partitionNumber;

            entryCache = manager.AllocLogEntryCache(recordsPerPartition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ToRelativeIndex(long absoluteIndex)
            => unchecked((int)(absoluteIndex - FirstIndex));

        [MemberNotNullWhen(false, nameof(Previous))]
        internal bool IsFirst => previous is null;

        [MemberNotNullWhen(false, nameof(Next))]
        internal bool IsLast => next is null;

        internal PartitionBase? Next => next;

        internal PartitionBase? Previous => previous;

        internal void Append(PartitionBase partition)
        {
            Debug.Assert(PartitionNumber < partition.PartitionNumber);
            partition.previous = this;
            partition.next = next;
            if (next is not null)
                next.previous = partition;
            next = partition;
        }

        internal void Prepend(PartitionBase partition)
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

        internal void DetachAscendant()
        {
            if (previous is not null)
                previous.next = null;
            previous = null;
        }

        internal void DetachDescendant()
        {
            if (next is not null)
                next.previous = null;
            next = null;
        }

        internal bool Contains(long recordIndex)
            => recordIndex >= FirstIndex && recordIndex <= LastIndex;

        internal abstract void Initialize();

        internal LogEntry Read(int sessionId, long absoluteIndex, bool metadataOnly = false)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            var relativeIndex = ToRelativeIndex(absoluteIndex);
            var metadata = GetMetadata(relativeIndex);

            ref readonly var cachedContent = ref EmptyRecord;

            if (metadataOnly)
                goto return_cached;

            if (!entryCache.IsEmpty)
                cachedContent = ref entryCache[relativeIndex];

            if (cachedContent.Content.IsEmpty && metadata.Length > 0L)
            {
                return new(in metadata, absoluteIndex)
                {
                    ContentReader = GetSessionReader(sessionId),
                    IsPersisted = true,
                };
            }

        return_cached:
            return new(in metadata, absoluteIndex)
            {
                ContentBuffer = cachedContent.Content.Memory,
                IsPersisted = cachedContent.PersistenceMode is not CachedLogEntryPersistenceMode.None,
            };
        }

        internal ValueTask PersistCachedEntryAsync(long absoluteIndex, bool removeFromMemory)
        {
            Debug.Assert(entryCache.IsEmpty is false);

            var index = ToRelativeIndex(absoluteIndex);
            Debug.Assert((uint)index < (uint)entryCache.Length);

            ref var cachedEntry = ref entryCache[index];
            Debug.Assert(cachedEntry.PersistenceMode is CachedLogEntryPersistenceMode.None);
            cachedEntry.PersistenceMode = CachedLogEntryPersistenceMode.CopyToBuffer;
            var offset = GetOffset(index);

            return cachedEntry.Content.IsEmpty
                ? ValueTask.CompletedTask
                : removeFromMemory
                ? PersistAndDeleteAsync(cachedEntry.Content.Memory, index, offset)
                : PersistAsync(cachedEntry.Content.Memory, offset);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask PersistAsync(ReadOnlyMemory<byte> content, long offset)
        {
            await SetWritePositionAsync(offset).ConfigureAwait(false);
            await writer.WriteAsync(content).ConfigureAwait(false);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask PersistAndDeleteAsync(ReadOnlyMemory<byte> content, int index, long offset)
        {
            try
            {
                // manually inlined body of PersistAsync method
                await SetWritePositionAsync(offset).ConfigureAwait(false);
                await writer.WriteAsync(content).ConfigureAwait(false);
            }
            finally
            {
                entryCache[index].Dispose();
            }
        }

        internal long GetTerm(long absoluteIndex)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            return GetMetadata(ToRelativeIndex(absoluteIndex)).Term;
        }

        private long GetOffset(int index)
            => GetMetadata(index).Offset;

        private void UpdateCache(in CachedLogEntry entry, int index)
        {
            Debug.Assert(entryCache.IsEmpty is false);
            Debug.Assert((uint)index < (uint)entryCache.Length);

            ref var cachedEntry = ref entryCache[index];
            cachedEntry.Dispose();
            cachedEntry = entry;
        }

        protected abstract LogEntryMetadata GetMetadata(int index);

        protected abstract ValueTask PersistAsync<TEntry>(TEntry entry, int index, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry;

        protected abstract ValueTask WriteThroughAsync(CachedLogEntry content, int index, CancellationToken token);

        protected abstract void OnCached(in CachedLogEntry cachedEntry, int index);

        internal ValueTask WriteAsync<TEntry>(TEntry entry, long absoluteIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            // write operation always expects absolute index so we need to convert it to the relative index
            var relativeIndex = ToRelativeIndex(absoluteIndex);
            Debug.Assert(absoluteIndex >= FirstIndex && relativeIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            if (typeof(TEntry) == typeof(CachedLogEntry))
            {
                ref readonly var cachedEntry = ref Unsafe.As<TEntry, CachedLogEntry>(ref entry);

                // fast path - just add cached log entry to the cache table
                UpdateCache(in cachedEntry, relativeIndex);

                // Perf: we can skip FileWriter internal buffer and write cached log entry directly to the disk
                switch (cachedEntry.PersistenceMode)
                {
                    case CachedLogEntryPersistenceMode.CopyToBuffer:
                        goto exit;
                    case CachedLogEntryPersistenceMode.SkipBuffer:
                        return WriteThroughAsync(cachedEntry, relativeIndex, token);
                    default:
                        OnCached(in cachedEntry, relativeIndex);
                        return ValueTask.CompletedTask;
                }
            }

            // invalidate cached log entry on write
            if (!entryCache.IsEmpty)
                entryCache[relativeIndex].Dispose();

            exit:
            return PersistAsync(entry, relativeIndex, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                previous = next = null;
                entryCache.ReleaseAll();
            }

            base.Dispose(disposing);
        }
    }

    // private protected sealed class SparsePartition : PartitionBase
    // {
    //     internal SparsePartition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
    //         : base(location, offset: 0, bufferSize, recordsPerPartition, partitionNumber, in manager, readersCount, writeMode, initialSize)
    //     {
    //         File.SetAttributes(Handle, FileAttributes.SparseFile);
    //     }

    //     protected override ValueTask WriteAsync<TEntry>(TEntry entry, int index, CancellationToken token)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }

    /*
        Partition file format:
        FileName - number of partition
        Payload:
        [struct LogEntryMetadata] X Capacity - prologue with metadata
        [octet string] X number of entries
     */
    private protected sealed class Partition : PartitionBase
    {
        // metadata management
        private MemoryOwner<byte> metadata;
        private int metadataFlushStartAddress;
        private int metadataFlushEndAddress;

        // represents offset within the file from which a newly added log entry payload can be recorded
        private long writeAddress;

        internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
            : base(location, checked(LogEntryMetadata.Size * recordsPerPartition), bufferSize, recordsPerPartition, partitionNumber, in manager, readersCount, writeMode, initialSize)
        {
            // allocate metadata segment
            metadata = manager.BufferAllocator.AllocateExactly(fileOffset);
            metadataFlushStartAddress = int.MaxValue;

            writeAddress = fileOffset;
        }

        internal override void Initialize()
        {
            using var handle = File.OpenHandle(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.SequentialScan);
            if (RandomAccess.Read(handle, metadata.Span, 0L) < fileOffset)
            {
                metadata.Span.Clear();
                RandomAccess.Write(handle, metadata.Span, 0L);
            }
            else
            {
                writeAddress = Math.Max(fileOffset, GetWriteAddress(metadata.Span));
            }

            static long GetWriteAddress(ReadOnlySpan<byte> metadataTable)
            {
                long result;

                for (result = 0L; !metadataTable.IsEmpty; metadataTable = metadataTable.Slice(LogEntryMetadata.Size))
                {
                    result = Math.Max(result, LogEntryMetadata.GetEndOfLogEntry(metadataTable));
                }

                return result;
            }
        }

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
        private Span<byte> GetMetadata(int index, out int offset)
        {
            Debug.Assert(metadata.Length == fileOffset);

            return metadata.Span.Slice(offset = index * LogEntryMetadata.Size);
        }

        protected override LogEntryMetadata GetMetadata(int index)
            => new(GetMetadata(index, out _));

        private void WriteMetadata(int index, in LogEntryMetadata metadata)
        {
            metadata.Format(GetMetadata(index, out var offset));

            metadataFlushStartAddress = Math.Min(metadataFlushStartAddress, offset);
            metadataFlushEndAddress = Math.Max(metadataFlushEndAddress, offset + LogEntryMetadata.Size);
        }

        protected override async ValueTask PersistAsync<TEntry>(TEntry entry, int index, CancellationToken token)
        {
            // slow path - persist log entry
            await SetWritePositionAsync(writeAddress, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);

            // save new log entry to the allocation table
            var length = writer.WritePosition - writeAddress;
            WriteMetadata(index, LogEntryMetadata.Create(entry, writeAddress, length));
            writeAddress += length;
        }

        protected override async ValueTask WriteThroughAsync(CachedLogEntry entry, int index, CancellationToken token)
        {
            await SetWritePositionAsync(writeAddress, token).ConfigureAwait(false);
            Debug.Assert(writer.HasBufferedData is false);

            await RandomAccess.WriteAsync(Handle, entry.Content.Memory, writeAddress, token).ConfigureAwait(false);

            // save new log entry to the allocation table
            WriteMetadata(index, LogEntryMetadata.Create(entry, writeAddress, entry.Length));
            writer.FilePosition = writeAddress += entry.Length;
        }

        protected override void OnCached(in CachedLogEntry cachedEntry, int index)
        {
            WriteMetadata(index, LogEntryMetadata.Create(cachedEntry, writeAddress, cachedEntry.Length));
            writeAddress += cachedEntry.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                metadata.Dispose();
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

    private protected readonly int recordsPerPartition;

    // Maintaining efficient data structure for a collection of partitions with the following characteristics:
    // 1. Committed partitions must be removed from the head of the list
    // 2. Uncommitted partitions must be removed from the tail of the list
    // 2. New partitions must be added to the tail of the list
    // 3. The list is sorted in ascending order (head is a partition with smaller number, tail is a partition with higher number)
    // 4. The thread that is responsible for removing partitions from the head (compaction thread) doesn't have
    // concurrency with the thread that is adding new partitions
    // Under the hood, this is simply a sorted linked list
    private protected PartitionBase? FirstPartition
    {
        get;
        private set;
    }

    private protected PartitionBase? LastPartition
    {
        get;
        private set;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

    private partial PartitionBase CreatePartition(long partitionNumber);

    // during insertion the index is growing monothonically so
    // this method is optimized for forward lookup in sorted list of partitions
    private void GetOrCreatePartition(long recordIndex, [NotNull] ref PartitionBase? partition)
    {
        var partitionNumber = PartitionOf(recordIndex);

        if (LastPartition is null)
        {
            Debug.Assert(FirstPartition is null);
            Debug.Assert(partition is null);
            FirstPartition = LastPartition = partition = CreatePartition(partitionNumber);
            goto exit;
        }

        Debug.Assert(FirstPartition is not null);
        partition ??= LastPartition;

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
                    if (partition.IsLast)
                    {
                        LastPartition = partition = Append(partitionNumber, partition);
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
                    if (partition.IsFirst)
                    {
                        FirstPartition = partition = Prepend(partitionNumber, partition);
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

        PartitionBase Prepend(long partitionNumber, PartitionBase partition)
        {
            var tmp = CreatePartition(partitionNumber);
            partition.Prepend(tmp);
            return tmp;
        }

        PartitionBase Append(long partitionNumber, PartitionBase partition)
        {
            var tmp = CreatePartition(partitionNumber);
            partition.Append(tmp);
            return tmp;
        }
    }

    private PartitionBase? TryGetPartition(long partitionNumber)
    {
        PartitionBase? result = LastPartition;
        if (result is null)
            goto exit;

        for (int previous = 0, current; ; previous = current)
        {
            switch (current = partitionNumber.CompareTo(result.PartitionNumber))
            {
                case > 0:
                    if (previous < 0 || result.IsLast)
                    {
                        result = null;
                        goto exit;
                    }

                    result = result.Next;
                    break;
                case < 0:
                    if (previous > 0 || result.IsFirst)
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
    private protected bool TryGetPartition(long recordIndex, [NotNullWhen(true)] ref PartitionBase? partition)
    {
        if (partition?.Contains(recordIndex) ?? false)
            goto success;

        if (LastPartition is null)
        {
            Debug.Assert(FirstPartition is null);
            Debug.Assert(partition is null);
            goto fail;
        }

        Debug.Assert(LastPartition is not null);
        partition ??= LastPartition;

        var partitionNumber = PartitionOf(recordIndex);

        for (int previous = 0, current; ; previous = current)
        {
            switch (current = partitionNumber.CompareTo(partition.PartitionNumber))
            {
                case > 0:
                    if (previous < 0 || partition.IsLast)
                        goto fail;

                    partition = partition.Next;
                    break;
                case < 0:
                    if (previous > 0 || partition.IsFirst)
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

    private static void DeletePartition(PartitionBase partition)
    {
        var fileName = partition.FileName;
        partition.Dispose();
        File.Delete(fileName);
    }

    // this method should be called for detached partition only
    private protected static void DeletePartitions(PartitionBase? head)
    {
        for (PartitionBase? next; head is not null; head = next)
        {
            next = head.Next;
            DeletePartition(head);
        }
    }

    private protected PartitionBase? DetachPartitions(long upperBoundIndex)
    {
        PartitionBase? result = FirstPartition, current;
        for (current = result; current is not null && current.LastIndex <= upperBoundIndex; current = current.Next);

        if (current is null)
        {
            FirstPartition = LastPartition = null;
        }
        else if (ReferenceEquals(current, result))
        {
            result = null;
        }
        else
        {
            current.DetachAscendant();
            FirstPartition = current;
        }

        return result;
    }

    private void InvalidatePartitions(long upToIndex)
    {
        for (PartitionBase? partition = LastPartition; partition is not null && partition.LastIndex >= upToIndex; partition = partition.Previous)
            partition.Invalidate();
    }
}
