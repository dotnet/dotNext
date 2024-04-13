using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IntegrityException = IO.Log.IntegrityException;

public partial class PersistentState
{
    private protected abstract class Partition : ConcurrentStorageAccess
    {
        internal const int MaxRecordsPerPartition = int.MaxValue / LogEntryMetadata.Size;
        protected static readonly CacheRecord EmptyRecord = new();

        internal readonly long FirstIndex, PartitionNumber, LastIndex;
        private Partition? previous, next;
        protected MemoryOwner<CacheRecord> entryCache;

        protected Partition(DirectoryInfo location, int offset, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize, FileAttributes attributes = FileAttributes.NotContentIndexed)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), offset, bufferSize, manager.BufferAllocator, readersCount, writeMode, initialSize, attributes)
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

        protected abstract ValueTask WriteThroughAsync(CachedLogEntry entry, int index, CancellationToken token);

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

    private sealed class SparsePartition : Partition, IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private readonly long maxLogEntrySize;
        private MemoryOwner<CachedLogEntryMetadata> metadataTable;
        private MemoryOwner<byte> metadataBuffer;
        private ReadOnlyMemory<byte> payloadBuffer;

        internal SparsePartition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize, long maxLogEntrySize)
            : base(location, offset: 0, bufferSize, recordsPerPartition, partitionNumber, in manager, readersCount, writeMode, initialSize, FileAttributes.NotContentIndexed | FileAttributes.SparseFile)
        {
            metadataTable = manager.Allocate<CachedLogEntryMetadata>(recordsPerPartition);
            metadataTable.Span.Clear(); // to prevent pre-filled objects

            this.maxLogEntrySize = maxLogEntrySize;
            metadataBuffer = manager.BufferAllocator.AllocateExactly(LogEntryMetadata.Size);
        }

        internal override void Initialize()
        {
            // do nothing
        }

        private long GetMetadataOffset(int index) => index * (maxLogEntrySize + LogEntryMetadata.Size);

        protected override LogEntryMetadata GetMetadata(int index)
        {
            ref var entry = ref metadataTable[index];

            if (!entry.IsLoaded)
            {
                // very rare so can be done synchronously
                Span<byte> buffer = stackalloc byte[LogEntryMetadata.Size];
                RandomAccess.Read(Handle, buffer, GetMetadataOffset(index));
                entry.Metadata = new(buffer);
            }

            return entry.Metadata;
        }

        protected override void OnCached(in CachedLogEntry cachedEntry, int index)
            => metadataTable[index].Metadata = LogEntryMetadata.Create(cachedEntry, GetMetadataOffset(index) + LogEntryMetadata.Size);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask PersistAsync<TEntry>(TEntry entry, int index, CancellationToken token)
        {
            var metadataOffset = GetMetadataOffset(index);
            LogEntryMetadata metadata;

            if (entry.Length is not { } length)
            {
                // slow path - write the entry first and then write metadata
                await SetWritePositionAsync(metadataOffset + LogEntryMetadata.Size, token).ConfigureAwait(false);
                length = writer.WritePosition;

                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                length = writer.WritePosition - length;

                if (length > maxLogEntrySize)
                    goto too_large_entry;

                metadata = LogEntryMetadata.Create(entry, metadataOffset + LogEntryMetadata.Size, length);
                metadata.Format(metadataBuffer.Span);
                await RandomAccess.WriteAsync(Handle, metadataBuffer.Memory, metadataOffset, token).ConfigureAwait(false);
            }
            else if (length <= maxLogEntrySize)
            {
                // fast path - length is known, metadata and the log entry can be written sequentially
                metadata = LogEntryMetadata.Create(entry, metadataOffset + LogEntryMetadata.Size, length);
                await SetWritePositionAsync(metadataOffset, token).ConfigureAwait(false);

                await writer.WriteAsync(metadata, token).ConfigureAwait(false);
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            }
            else
            {
                goto too_large_entry;
            }

            metadataTable[index].Metadata = metadata;
            return;

        too_large_entry:
            throw new InvalidOperationException(ExceptionMessages.LogEntryPayloadTooLarge);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask WriteThroughAsync(CachedLogEntry entry, int index, CancellationToken token)
        {
            if (entry.Length > maxLogEntrySize)
                throw new InvalidOperationException(ExceptionMessages.LogEntryPayloadTooLarge);

            var metadata = LogEntryMetadata.Create(entry, GetMetadataOffset(index) + LogEntryMetadata.Size);
            metadata.Format(metadataBuffer.Span);

            payloadBuffer = entry.Content.Memory;
            await RandomAccess.WriteAsync(Handle, this, GetMetadataOffset(index), token).ConfigureAwait(false);
            payloadBuffer = default;
            metadataTable[index].Metadata = metadata;
        }

        ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index] => index switch
        {
            0 => metadataBuffer.Memory,
            1 => payloadBuffer,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

        private IEnumerator<ReadOnlyMemory<byte>> GetEnumerator()
        {
            yield return metadataBuffer.Memory;
            yield return payloadBuffer;
        }

        IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            metadataTable.Dispose();
            metadataBuffer.Dispose();
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Auto)]
        private struct CachedLogEntryMetadata
        {
            private LogEntryMetadata metadata;
            private volatile bool loaded;

            internal readonly bool IsLoaded => loaded;

            internal LogEntryMetadata Metadata
            {
                readonly get => metadata;
                set
                {
                    metadata = value;
                    loaded = true;
                }
            }
        }
    }

    /*
        Partition file format:
        FileName - number of partition
        Payload:
        [512 bytes] - header:
            [1 byte] - true if completed partition
        [struct LogEntryMetadata] [octet string] X Capacity - log entries prefixed with metadata
        [struct LogEntryMetadata] X Capacity - a table of log entries within the file, if partition is completed
     */
    private sealed class Table : Partition, IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private const int HeaderSize = 512;

        // metadata management
        private MemoryOwner<byte> header, footer, metadataBuffer;
        private ReadOnlyMemory<byte> payloadBuffer;

        internal Table(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
            : base(location, HeaderSize, bufferSize, recordsPerPartition, partitionNumber, in manager, readersCount, writeMode, initialSize)
        {
            footer = manager.BufferAllocator.AllocateExactly(recordsPerPartition * LogEntryMetadata.Size);

            header = manager.BufferAllocator.AllocateExactly(HeaderSize);
            header.Span.Clear();

            metadataBuffer = manager.BufferAllocator.AllocateExactly(LogEntryMetadata.Size);

            // init ephemeral 0 entry
            if (PartitionNumber is 0L)
            {
                var metadata = LogEntryMetadata.Create(LogEntry.Initial, HeaderSize + LogEntryMetadata.Size, length: 0L);
                metadata.Format(footer.Span);
            }
        }

        private bool IsSealed
        {
            get => Unsafe.BitCast<byte, bool>(MemoryMarshal.GetReference(header.Span));
            set => MemoryMarshal.GetReference(header.Span) = Unsafe.BitCast<bool, byte>(value);
        }

        internal override void Initialize()
        {
            using var handle = File.OpenHandle(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.SequentialScan);

            // read header
            if (RandomAccess.Read(Handle, header.Span, fileOffset: 0L) < HeaderSize)
            {
                header.Span.Clear();
            }
            else if (IsSealed)
            {
                // partition is completed, read table
                var tableStart = RandomAccess.GetLength(Handle);
                RandomAccess.Read(Handle, footer.Span, tableStart - footer.Length);
            }
            else
            {
                // read sequentially every log entry
                var metadataBuffer = this.metadataBuffer.Span;
                var metadataTable = footer.Span;
                int footerOffset = 0;
                for (long fileOffset = HeaderSize; ; footerOffset += LogEntryMetadata.Size)
                {
                    var count = RandomAccess.Read(Handle, metadataBuffer, fileOffset);
                    if (count < LogEntryMetadata.Size)
                        break;

                    fileOffset = LogEntryMetadata.GetEndOfLogEntry(metadataBuffer);
                    if (fileOffset is 0L)
                        break;

                    metadataBuffer.CopyTo(metadataTable.Slice(footerOffset, LogEntryMetadata.Size));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> GetMetadataSpan(int index)
            => footer.Span.Slice(index * LogEntryMetadata.Size, LogEntryMetadata.Size);

        protected override LogEntryMetadata GetMetadata(int index)
            => new(GetMetadataSpan(index));

        private long GetWriteAddress(int index)
            => index is 0 ? fileOffset : LogEntryMetadata.GetEndOfLogEntry(GetMetadataSpan(index - 1));

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask PersistAsync<TEntry>(TEntry entry, int index, CancellationToken token)
        {
            var writeAddress = GetWriteAddress(index);

            LogEntryMetadata metadata;
            var startPos = writeAddress + LogEntryMetadata.Size;
            if (entry.Length is { } length)
            {
                // fast path - write metadata and entry sequentially
                metadata = LogEntryMetadata.Create(entry, startPos, length);

                await SetWritePositionAsync(writeAddress, token).ConfigureAwait(false);
                await writer.WriteAsync(metadata, token).ConfigureAwait(false);
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            }
            else
            {
                // slow path - write entry first
                await SetWritePositionAsync(startPos, token).ConfigureAwait(false);

                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                length = writer.WritePosition - startPos;

                metadata = LogEntryMetadata.Create(entry, startPos, length);
                metadata.Format(metadataBuffer.Span);
                await RandomAccess.WriteAsync(Handle, metadataBuffer.Memory, writeAddress, token).ConfigureAwait(false);
            }

            metadata.Format(GetMetadataSpan(index));

            if (index == LastIndex)
            {
                // write footer with metadata table
                await RandomAccess.WriteAsync(Handle, footer.Memory, metadata.End, token).ConfigureAwait(false);
                RandomAccess.SetLength(Handle, metadata.End + footer.Length);

                // seal the partition
                IsSealed = true;
            }
            else if (IsSealed)
            {
                // unseal
                IsSealed = false;
            }
            else
            {
                return;
            }

            await RandomAccess.WriteAsync(Handle, header.Memory, fileOffset: 0L, token).ConfigureAwait(false);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask WriteThroughAsync(CachedLogEntry entry, int index, CancellationToken token)
        {
            var writeAddress = GetWriteAddress(index);
            var startPos = writeAddress + LogEntryMetadata.Size;
            var metadata = LogEntryMetadata.Create(entry, startPos, entry.Length);
            metadata.Format(metadataBuffer.Span);

            payloadBuffer = entry.Content.Memory;
            await RandomAccess.WriteAsync(Handle, this, writeAddress, token).ConfigureAwait(false);
            payloadBuffer = default;

            metadata.Format(GetMetadataSpan(index));

            if (index == LastIndex)
            {
                // write footer with metadata table
                await RandomAccess.WriteAsync(Handle, footer.Memory, metadata.End, token).ConfigureAwait(false);
                RandomAccess.SetLength(Handle, metadata.End + footer.Length);

                // seal the partition
                IsSealed = true;
            }
            else if (IsSealed)
            {
                // unseal
                IsSealed = false;
            }
            else
            {
                return;
            }

            await RandomAccess.WriteAsync(Handle, header.Memory, fileOffset: 0L, token).ConfigureAwait(false);
        }

        protected override void OnCached(in CachedLogEntry cachedEntry, int index)
        {
            var startPos = GetWriteAddress(index) + LogEntryMetadata.Size;
            var metadata = LogEntryMetadata.Create(in cachedEntry, startPos);
            metadata.Format(GetMetadataSpan(index));
        }

        ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index] => index switch
        {
            0 => metadataBuffer.Memory,
            1 => payloadBuffer,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

        private IEnumerator<ReadOnlyMemory<byte>> GetEnumerator()
        {
            yield return metadataBuffer.Memory;
            yield return payloadBuffer;
        }

        IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            header.Dispose();
            footer.Dispose();
            metadataBuffer.Dispose();
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
    private protected Partition? FirstPartition
    {
        get;
        private set;
    }

    private protected Partition? LastPartition
    {
        get;
        private set;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

    private partial Partition CreatePartition(long partitionNumber);

    // during insertion the index is growing monothonically so
    // this method is optimized for forward lookup in sorted list of partitions
    private void GetOrCreatePartition(long recordIndex, [NotNull] ref Partition? partition)
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
        Partition? result = LastPartition;
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
    private protected bool TryGetPartition(long recordIndex, [NotNullWhen(true)] ref Partition? partition)
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

    private static void DeletePartition(Partition partition)
    {
        var fileName = partition.FileName;
        partition.Dispose();
        File.Delete(fileName);
    }

    // this method should be called for detached partition only
    private protected static void DeletePartitions(Partition? head)
    {
        for (Partition? next; head is not null; head = next)
        {
            next = head.Next;
            DeletePartition(head);
        }
    }

    private protected Partition? DetachPartitions(long upperBoundIndex)
    {
        Partition? result = FirstPartition, current;
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
        for (Partition? partition = LastPartition; partition is not null && partition.LastIndex >= upToIndex; partition = partition.Previous)
            partition.Invalidate();
    }
}
