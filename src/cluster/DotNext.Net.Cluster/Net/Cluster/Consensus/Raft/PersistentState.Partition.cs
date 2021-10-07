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
        private const string MetadataTableFileExtension = "meta";
        private static readonly MemoryOwner<byte> EmptyBuffer;

        internal readonly long FirstIndex, PartitionNumber, LastIndex;
        internal readonly string MetadataTableFileName;
        private readonly MemoryMappedFile metadataFile;
        private readonly MemoryMappedViewAccessor metadataFileAccessor;
        private MemoryOwner<MemoryOwner<byte>> entryCache;
        private Partition? previous, next;

        internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, bool writeThrough, long initialSize)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize, manager.BufferAllocator, readersCount, GetOptions(writeThrough), initialSize)
        {
            FirstIndex = partitionNumber * recordsPerPartition;
            LastIndex = FirstIndex + recordsPerPartition - 1L;
            PartitionNumber = partitionNumber;

            // create metadata file
            MetadataTableFileName = Path.ChangeExtension(FileName, MetadataTableFileExtension);
            var metadataTableSize = Math.BigMul(LogEntryMetadata.Size, recordsPerPartition);
            metadataFile = MemoryMappedFile.CreateFromFile(MetadataTableFileName, FileMode.OpenOrCreate, null, metadataTableSize, MemoryMappedFileAccess.ReadWrite);
            metadataFileAccessor = metadataFile.CreateViewAccessor(0L, metadataTableSize);
            entryCache = manager.AllocLogEntryCache(recordsPerPartition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileOptions GetOptions(bool writeThrough)
        {
            const FileOptions skipBufferOptions = FileOptions.SequentialScan | FileOptions.WriteThrough | FileOptions.Asynchronous;
            const FileOptions dontSkipBufferOptions = FileOptions.SequentialScan | FileOptions.Asynchronous;
            return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nint ToRelativeIndex(long absoluteIndex)
            => (nint)(absoluteIndex - FirstIndex);

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

        public override ValueTask FlushAsync(CancellationToken token = default)
        {
            try
            {
                metadataFileAccessor.Flush();
            }
            catch (Exception e)
            {
                return ValueTask.FromException(e);
            }

            return base.FlushAsync(token);
        }

        private static ref byte GetMetadata(nint index, SafeBuffer buffer)
        {
            ref var ptr = ref buffer.AcquirePointer();
            if (!Unsafe.IsNullRef(ref ptr))
                ptr = ref Unsafe.Add(ref ptr, index * LogEntryMetadata.Size);

            return ref ptr;
        }

        private void ReadMetadata(nint index, out LogEntryMetadata metadata)
        {
            var handle = metadataFileAccessor.SafeMemoryMappedViewHandle;
            var acquired = false;
            try
            {
                var reader = new SpanReader<byte>(ref GetMetadata(index, handle), LogEntryMetadata.Size);
                acquired = true;
                metadata = new(ref reader);
            }
            finally
            {
                if (acquired)
                    handle.ReleasePointer();
            }
        }

        private void WriteMetadata(nint index, in LogEntryMetadata metadata)
        {
            var handle = metadataFileAccessor.SafeMemoryMappedViewHandle;
            var acquired = false;
            try
            {
                var reader = new SpanWriter<byte>(ref GetMetadata(index, handle), LogEntryMetadata.Size);
                acquired = true;
                metadata.Format(ref reader);
            }
            finally
            {
                if (acquired)
                    handle.ReleasePointer();
            }
        }

        internal long GetTerm(long absoluteIndex)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            var relativeIndex = ToRelativeIndex(absoluteIndex);
            ReadMetadata(relativeIndex, out var metadata);
            return metadata.Term;
        }

        internal LogEntry Read(int sessionId, long absoluteIndex, LogEntryReadOptimizationHint hint = LogEntryReadOptimizationHint.None)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            var relativeIndex = ToRelativeIndex(absoluteIndex);
            ReadMetadata(relativeIndex, out var metadata);

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

        private void UpdateCache(in CachedLogEntry entry, nint index, long offset, out LogEntryMetadata metadata)
        {
            Debug.Assert(entryCache.IsEmpty is false);
            Debug.Assert(index >= 0 && index < entryCache.Length);

            ref var cacheEntry = ref entryCache[index];
            cacheEntry.Dispose();
            cacheEntry = entry.Content;
            metadata = LogEntryMetadata.Create(in entry, offset);
        }

        internal async ValueTask PersistCachedEntryAsync(long absoluteIndex, long offset, bool removeFromMemory)
        {
            Debug.Assert(entryCache.IsEmpty is false);

            var index = ToRelativeIndex(absoluteIndex);
            Debug.Assert(index >= 0 && index < entryCache.Length);

            var content = entryCache[index].Memory;
            if (!content.IsEmpty)
            {
                try
                {
                    await SetWritePositionAsync(offset).ConfigureAwait(false);
                    await writer.WriteAsync(content).ConfigureAwait(false);
                }
                finally
                {
                    if (removeFromMemory)
                        entryCache[index].Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFirstEntry(nint index)
            => index == 0 || index == 1 && FirstIndex == 0L;

        internal override async ValueTask WriteAsync<TEntry>(TEntry entry, long absoluteIndex, CancellationToken token = default)
        {
            // write operation always expects absolute index so we need to convert it to the relative index
            var relativeIndex = ToRelativeIndex(absoluteIndex);
            Debug.Assert(absoluteIndex >= FirstIndex && relativeIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            // calculate offset of the previous entry
            long offset;
            LogEntryMetadata metadata;
            if (IsFirstEntry(relativeIndex))
            {
                offset = 0L;
            }
            else
            {
                ReadMetadata(relativeIndex - 1, out metadata);
                offset = metadata.Length + metadata.Offset;
            }

            if (typeof(TEntry) == typeof(CachedLogEntry))
            {
                // fast path - just add cached log entry to the cache table
                UpdateCache(in Unsafe.As<TEntry, CachedLogEntry>(ref entry), relativeIndex, offset, out metadata);
            }
            else
            {
                // slow path - persist log entry
                await SetWritePositionAsync(offset, token).ConfigureAwait(false);
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                metadata = LogEntryMetadata.Create(entry, offset, writer.WritePosition - offset);
            }

            // save new log entry to the allocation table
            WriteMetadata(relativeIndex, in metadata);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                metadataFileAccessor.Dispose();
                metadataFile.Dispose();
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

        private SnapshotMetadata metadata;

        internal Snapshot(DirectoryInfo location, int bufferSize, in BufferManager manager, int readersCount, bool writeThrough, bool tempSnapshot = false, long initialSize = 0L)
            : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), bufferSize, manager.BufferAllocator, readersCount, GetOptions(writeThrough), initialSize)
        {
        }

        // cache flag that allows to avoid expensive access to Length that can cause native call
        internal bool IsEmpty => metadata.Index == 0L;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileOptions GetOptions(bool writeThrough)
        {
            const FileOptions skipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess | FileOptions.WriteThrough;
            const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess;
            return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
        }

        internal void Initialize()
        {
            using var task = InitializeAsync().AsTask();
            task.Wait();
        }

        internal async ValueTask InitializeAsync()
            => metadata = FileSize >= SnapshotMetadata.Size ? await GetSessionReader(0).ParseAsync<SnapshotMetadata>().ConfigureAwait(false) : default;

        internal async ValueTask WriteMetadataAsync(long index, DateTimeOffset timestamp, long term, CancellationToken token = default)
        {
            await SetWritePositionAsync(0L, token).ConfigureAwait(false);
            metadata = new(index, timestamp, term, FileSize - SnapshotMetadata.Size);
            await writer.WriteFormattableAsync(metadata, token).ConfigureAwait(false);
        }

        internal override async ValueTask WriteAsync<TEntry>(TEntry entry, long index, CancellationToken token = default)
        {
            await SetWritePositionAsync(SnapshotMetadata.Size, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            metadata = SnapshotMetadata.Create(entry, index, writer.WritePosition - SnapshotMetadata.Size);
            await SetWritePositionAsync(0L, token).ConfigureAwait(false);
            await writer.WriteFormattableAsync(metadata, token).ConfigureAwait(false);
        }

        // optimization hint is not supported for snapshots
        internal LogEntry Read(int sessionId)
            => new LogEntry(GetSessionReader(sessionId), in metadata);

        // cached index of the snapshotted entry
        internal ref readonly SnapshotMetadata Metadata => ref metadata;
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
        string fileName = partition.FileName, metadataTableFileName = partition.MetadataTableFileName;
        partition.Dispose();
        File.Delete(fileName);
        File.Delete(metadataTableFileName);
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
