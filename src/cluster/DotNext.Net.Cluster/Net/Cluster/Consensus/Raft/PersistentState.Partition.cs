using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using static Runtime.InteropServices.SafeBufferExtensions;
    using IntegrityException = IO.Log.IntegrityException;

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
            internal readonly long FirstIndex, PartitionNumber;
            internal readonly int Capacity;    // max number of entries
            private readonly MemoryMappedFile metadataFile;
            private readonly MemoryMappedViewAccessor metadataFileAccessor;
            private MemoryOwner<IMemoryOwner<byte>?> entryCache;
            private Partition? previous, next;

            // cached position for write to avoid expensive native calls
            private long writePosition;

            internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, bool writeThrough, long initialSize)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize, readersCount, GetOptions(writeThrough), initialSize)
            {
                Capacity = recordsPerPartition;
                FirstIndex = partitionNumber * recordsPerPartition;
                PartitionNumber = partitionNumber;

                // create metadata file
                MetadataTableFileName = Path.ChangeExtension(FileName, MetadataTableFileExtension);
                metadataFile = MemoryMappedFile.CreateFromFile(MetadataTableFileName, FileMode.OpenOrCreate, null, MetadataTableSize, MemoryMappedFileAccess.ReadWrite);
                metadataFileAccessor = metadataFile.CreateViewAccessor(0L, MetadataTableSize);
                entryCache = manager.AllocLogEntryCache(recordsPerPartition);
            }

            internal string MetadataTableFileName { get; }

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

            private long MetadataTableSize => Math.BigMul(LogEntryMetadata.Size, Capacity);

            internal long LastIndex => FirstIndex + Capacity - 1;

            internal bool Contains(long recordIndex)
                => recordIndex >= FirstIndex && recordIndex <= LastIndex;

            public override Task FlushAsync(CancellationToken token = default)
            {
                try
                {
                    metadataFileAccessor.Flush();
                }
                catch (Exception e)
                {
                    return Task.FromException(e);
                }

                return base.FlushAsync(token);
            }

            public override void Flush()
            {
                metadataFileAccessor.Flush();
                base.Flush();
            }

            private static Span<byte> GetMetadata(nint index, SafeBuffer buffer, out bool acquired)
            {
                ref var ptr = ref Unsafe.NullRef<byte>();
                ptr = ref buffer.AcquirePointer();
                acquired = !Unsafe.IsNullRef(ref ptr);
                return MemoryMarshal.CreateSpan(ref Unsafe.Add(ref ptr, index * LogEntryMetadata.Size), LogEntryMetadata.Size);
            }

            private void ReadMetadata(nint index, out LogEntryMetadata metadata)
            {
                var handle = metadataFileAccessor.SafeMemoryMappedViewHandle;
                var acquired = false;
                try
                {
                    var reader = new SpanReader<byte>(GetMetadata(index, handle, out acquired));
                    metadata = new LogEntryMetadata(ref reader);
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
                    var reader = new SpanWriter<byte>(GetMetadata(index, handle, out acquired));
                    metadata.Serialize(ref reader);
                }
                finally
                {
                    if (acquired)
                        handle.ReleasePointer();
                }
            }

            // completed synchronously if metadata is cached
            private LogEntry Read(StreamSegment reader, Memory<byte> buffer, nint relativeIndex, long absoluteIndex)
            {
                Debug.Assert(relativeIndex >= 0 && relativeIndex < Capacity, $"Invalid index value {relativeIndex}, offset {FirstIndex}");

                ReadMetadata(relativeIndex, out var metadata);
                var cachedContent = entryCache.IsEmpty ? null : entryCache[relativeIndex];
                return cachedContent is null ?
                    new(reader, buffer, metadata, absoluteIndex) :
                    new(cachedContent, metadata, absoluteIndex);
            }

            // We don't need to analyze read optimization hint.
            // Metadata reconstruction is cheap operation (especially if metadata cache is enabled).
            internal LogEntry Read(in DataAccessSession session, long index, bool absoluteIndex)
            {
                // calculate relative index
                nint relativeIndex;
                if (absoluteIndex)
                {
                    relativeIndex = ToRelativeIndex(index);
                }
                else
                {
                    relativeIndex = (nint)index;
                    index += FirstIndex;
                }

                return Read(GetReadSessionStream(in session), session.Buffer, relativeIndex, index);
            }

            private void UpdateCache(in CachedLogEntry entry, nint index)
            {
                Debug.Assert(entryCache.IsEmpty is false);
                Debug.Assert(index >= 0 && index < entryCache.Length);

                ref var cacheEntry = ref entryCache[index];
                cacheEntry?.Dispose();
                cacheEntry = entry.Content;
            }

            private void SetPosition(long value)
            {
                if (value != writePosition)
                {
                    writePosition = value;
                    Position = value;
                }
            }

            internal async ValueTask PersistCachedEntryAsync(long absoluteIndex, long offset, bool removeFromMemory)
            {
                Debug.Assert(entryCache.IsEmpty is false);

                var index = ToRelativeIndex(absoluteIndex);
                Debug.Assert(index >= 0 && index < entryCache.Length);

                var content = entryCache[index];
                if (content is not null)
                {
                    try
                    {
                        SetPosition(offset);
                        await WriteAsync(content.Memory).ConfigureAwait(false);

                        // flush only internal buffer without flushing metadata table because
                        // it remains unchanged
                        await base.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (removeFromMemory)
                        {
                            entryCache[index] = null;
                            content.Dispose();
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsFirstEntry(nint index)
                => index == 0 || index == 1 && FirstIndex == 0L;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal async ValueTask WriteAsync<TEntry>(DataAccessSession session, TEntry entry, long absoluteIndex)
                where TEntry : notnull, IRaftLogEntry
            {
                // write operation always expects absolute index so we need to convert it to the relative index
                var relativeIndex = ToRelativeIndex(absoluteIndex);
                Debug.Assert(relativeIndex >= 0 && relativeIndex < Capacity, $"Invalid index value {relativeIndex}, offset {FirstIndex}");

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
                    offset = metadata.End;
                }

                if (typeof(TEntry) == typeof(CachedLogEntry))
                {
                    // fast path - just add cached log entry to the cache table
                    UpdateCache(in Unsafe.As<TEntry, CachedLogEntry>(ref entry), relativeIndex);
                    metadata = LogEntryMetadata.Create(in Unsafe.As<TEntry, CachedLogEntry>(ref entry), offset);
                }
                else
                {
                    // slow path - persist log entry
                    SetPosition(offset);
                    await entry.WriteToAsync(this, session.Buffer).ConfigureAwait(false);
                    metadata = LogEntryMetadata.Create(entry, offset, Position - offset);
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
        private sealed class Snapshot : ConcurrentStorageAccess
        {
            private new const string FileName = "snapshot";
            private const string TempFileName = "snapshot.new";

            internal Snapshot(DirectoryInfo location, int bufferSize, int readersCount, bool writeThrough, bool tempSnapshot = false)
                : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), bufferSize, readersCount, GetOptions(writeThrough), 0L, out var actualLength)
            {
                IsEmpty = actualLength == 0L;
            }

            // cache flag that allows to avoid expensive access to Length that can cause native call
            internal bool IsEmpty
            {
                get;
                private set;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static FileOptions GetOptions(bool writeThrough)
            {
                const FileOptions skipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess | FileOptions.WriteThrough;
                const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess;
                return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
            }

            internal void Initialize()
                => Index = IsEmpty ? 0L : ReadMetadata(this).Index;

            private static SnapshotMetadata ReadMetadata(Stream input)
            {
                Span<byte> buffer = stackalloc byte[SnapshotMetadata.Size];
                input.ReadBlock(buffer);
                var reader = new SpanReader<byte>(buffer);
                return new SnapshotMetadata(ref reader);
            }

            private static async ValueTask<SnapshotMetadata> ReadMetadataAsync(Stream input, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, SnapshotMetadata.Size);
                await input.ReadBlockAsync(buffer, token).ConfigureAwait(false);
                return Deserialize(buffer.Span);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static SnapshotMetadata Deserialize(Span<byte> metadata)
                {
                    var reader = new SpanReader<byte>(metadata);
                    return new SnapshotMetadata(ref reader);
                }
            }

            private static ValueTask WriteMetadataAsync(Stream output, in SnapshotMetadata metadata, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, SnapshotMetadata.Size);
                var writer = new SpanWriter<byte>(buffer.Span);
                metadata.Serialize(ref writer);
                return output.WriteAsync(buffer, token);
            }

            private async ValueTask WriteAsync<TEntry>(TEntry entry, long index, Memory<byte> buffer, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
            {
                Index = index;
                IsEmpty = false;
                Position = SnapshotMetadata.Size;
                await entry.WriteToAsync(this, buffer, token).ConfigureAwait(false);
                var metadata = SnapshotMetadata.Create(entry, index, Length - SnapshotMetadata.Size);
                Position = 0L;
                await WriteMetadataAsync(this, metadata, buffer, token).ConfigureAwait(false);
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index, CancellationToken token = default)
                where TEntry : notnull, IRaftLogEntry
                => WriteAsync(entry, index, session.Buffer, token);

            private static async ValueTask<LogEntry> ReadAsync(StreamSegment reader, Memory<byte> buffer, CancellationToken token)
            {
                reader.BaseStream.Position = 0L;
                return new LogEntry(reader, buffer, await ReadMetadataAsync(reader.BaseStream, buffer, token).ConfigureAwait(false));
            }

            // optimization hint is not supported for snapshots
            internal ValueTask<LogEntry> ReadAsync(in DataAccessSession session, CancellationToken token)
                => ReadAsync(GetReadSessionStream(session), session.Buffer, token);

            // cached index of the snapshotted entry
            internal long Index
            {
                get;
                private set;
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

        private readonly int recordsPerPartition;

        // Maintaining efficient data structure for a collection of partitions with the following characteristics:
        // 1. Committed partitions must be removed from the head of the list
        // 2. Uncommitted partitions must be removed at the tail of the list
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

        private Task GetOrCreatePartitionAsync(long recordIndex, [NotNull] ref Partition? partition)
        {
            var flushTask = Task.CompletedTask;

            switch (partition)
            {
                case not null when !partition.Contains(recordIndex):
                    flushTask = partition.FlushAsync();
                    goto case null;
                case null:
                    GetOrCreatePartition(recordIndex, ref partition);
                    break;
            }

            return flushTask;
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

        private static async ValueTask DeletePartitionAsync(Partition partition)
        {
            string fileName = partition.FileName, metadataTableFileName = partition.MetadataTableFileName;
            await partition.DisposeAsync().ConfigureAwait(false);
            File.Delete(fileName);
            File.Delete(metadataTableFileName);
        }

        // this method should be called for detached partition head only
        private static async ValueTask DeletePartitionsAsync(Partition? current)
        {
            for (Partition? next; current is not null; current = next)
            {
                next = current.Next;
                await DeletePartitionAsync(current).ConfigureAwait(false);
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
    }
}
