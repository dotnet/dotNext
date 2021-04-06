using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using IntegrityException = IO.Log.IntegrityException;
    using LogEntryReadOptimizationHint = IO.Log.LogEntryReadOptimizationHint;

    public partial class PersistentState
    {
        /*
            Partition file format:
            FileName - number of partition
            Allocation table:
            [struct LogEntryMetadata] X number of entries
            Payload:
            [octet string] X number of entries
         */
        private sealed class Partition : ConcurrentStorageAccess
        {
            internal readonly long FirstIndex, PartitionNumber;
            internal readonly int Capacity;    // max number of entries
            private MemoryOwner<LogEntryMetadata> lookupCache;
            private Partition? previous, next;

            internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, MemoryAllocator<LogEntryMetadata>? cachePool, int readersCount, bool writeThrough)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize, readersCount, GetOptions(writeThrough))
            {
                Capacity = recordsPerPartition;
                FirstIndex = partitionNumber * recordsPerPartition;
                PartitionNumber = partitionNumber;
                lookupCache = cachePool is null ? default : cachePool(recordsPerPartition);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static FileOptions GetOptions(bool writeThrough)
            {
                const FileOptions skipBufferOptions = FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous;
                const FileOptions dontSkipBufferOptions = FileOptions.RandomAccess | FileOptions.Asynchronous;
                return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
            }

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

            private long PayloadOffset => Math.BigMul(LogEntryMetadata.Size, Capacity);

            internal long LastIndex => FirstIndex + Capacity - 1;

            internal void Allocate(long initialSize) => SetLength(initialSize + PayloadOffset);

            private void PopulateCache(Span<byte> buffer, Span<LogEntryMetadata> lookupCache)
            {
                for (int index = 0, count; index < lookupCache.Length; index += count)
                {
                    count = Math.Min(buffer.Length / LogEntryMetadata.Size, lookupCache.Length - index);
                    var maxBytes = count * LogEntryMetadata.Size;
                    var source = buffer.Slice(0, maxBytes);
                    if (Read(source) < maxBytes)
                        throw new EndOfStreamException();
                    var destination = AsBytes(lookupCache.Slice(index));
                    source.CopyTo(destination);
                }
            }

            internal bool Contains(long recordIndex)
                => recordIndex >= FirstIndex && recordIndex <= LastIndex;

            internal override void PopulateCache(in DataAccessSession session)
            {
                if (!lookupCache.IsEmpty)
                    PopulateCache(session.Buffer.Span, lookupCache.Memory.Span.Slice(0, Capacity));
            }

            private static async ValueTask<LogEntryMetadata> ReadMetadataAsync(Stream input, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, LogEntryMetadata.Size);
                await input.ReadBlockAsync(buffer, token).ConfigureAwait(false);
                return LogEntryMetadata.Deserialize(buffer.Span);
            }

            private static ValueTask WriteMetadataAsync(Stream output, in LogEntryMetadata metadata, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, LogEntryMetadata.Size);
                metadata.Serialize(buffer.Span);
                return output.WriteAsync(buffer, token);
            }

            private async ValueTask<LogEntry> ReadAsync(StreamSegment reader, Memory<byte> buffer, nint relativeIndex, long absoluteIndex, CancellationToken token)
            {
                Debug.Assert(relativeIndex >= 0 && relativeIndex < Capacity, $"Invalid index value {relativeIndex}, offset {FirstIndex}");

                // find pointer to the content
                LogEntryMetadata metadata;
                if (lookupCache.IsEmpty)
                {
                    reader.BaseStream.Position = (long)relativeIndex * LogEntryMetadata.Size;
                    metadata = await ReadMetadataAsync(reader.BaseStream, buffer, token).ConfigureAwait(false);
                }
                else
                {
                    metadata = lookupCache[relativeIndex];
                }

                return metadata.IsValid ? new LogEntry(reader, buffer, metadata, absoluteIndex) : throw new MissingLogEntryException(relativeIndex, FirstIndex, LastIndex, FileName);
            }

            // We don't need to analyze read optimization hint.
            // Metadata reconstruction is cheap operation (especially if metadata cache is enabled).
            internal ValueTask<LogEntry> ReadAsync(in DataAccessSession session, long index, bool absoluteIndex, CancellationToken token)
            {
                // calculate relative index
                nint relativeIndex;
                if (absoluteIndex)
                {
                    relativeIndex = (nint)(index - FirstIndex);
                }
                else
                {
                    relativeIndex = (nint)index;
                    index += FirstIndex;
                }

                return ReadAsync(GetReadSessionStream(in session), session.Buffer, relativeIndex, index, token);
            }

            private async ValueTask WriteAsync<TEntry>(TEntry entry, nint index, Memory<byte> buffer)
                where TEntry : notnull, IRaftLogEntry
            {
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");

                // calculate offset of the previous entry
                long offset;
                LogEntryMetadata metadata;
                if (index == 0 || index == 1 && FirstIndex == 0L)
                {
                    offset = PayloadOffset;
                }
                else if (lookupCache.IsEmpty)
                {
                    // read content offset and the length of the previous entry
                    Position = (index - 1L) * LogEntryMetadata.Size;
                    metadata = await ReadMetadataAsync(this, buffer).ConfigureAwait(false);
                    Debug.Assert(metadata.Offset > 0, "Previous entry doesn't exist for unknown reason");
                    offset = metadata.Length + metadata.Offset;
                }
                else
                {
                    metadata = lookupCache[index - 1];
                    Debug.Assert(metadata.Offset > 0, "Previous entry doesn't exist for unknown reason");
                    offset = metadata.Length + metadata.Offset;
                }

                // write content
                Position = offset;
                await entry.WriteToAsync(this, buffer).ConfigureAwait(false);
                metadata = LogEntryMetadata.Create(entry, offset, Position - offset);

                // record new log entry to the allocation table
                Position = (long)index * LogEntryMetadata.Size;
                await WriteMetadataAsync(this, metadata, buffer).ConfigureAwait(false);

                // update cache
                if (!lookupCache.IsEmpty)
                    lookupCache[index] = metadata;
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long absoluteIndex)
                where TEntry : notnull, IRaftLogEntry
            {
                // write operation always expects absolute index so we need to convert it to the relative index
                return WriteAsync(entry, (nint)(absoluteIndex - FirstIndex), session.Buffer);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    lookupCache.Dispose();
                    lookupCache = default;
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
                : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), bufferSize, readersCount, GetOptions(writeThrough))
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static FileOptions GetOptions(bool writeThrough)
            {
                const FileOptions skipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess | FileOptions.WriteThrough;
                const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.RandomAccess;
                return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
            }

            internal override void PopulateCache(in DataAccessSession session)
                => Index = Length > 0L ? this.Read<SnapshotMetadata>().Index : 0L;

            private static async ValueTask<SnapshotMetadata> ReadMetadataAsync(Stream input, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, SnapshotMetadata.Size);
                await input.ReadAsync(buffer, token).ConfigureAwait(false);
                return SnapshotMetadata.Deserialize(buffer.Span);
            }

            private static ValueTask WriteMetadataAsync(Stream output, in SnapshotMetadata metadata, Memory<byte> buffer, CancellationToken token = default)
            {
                buffer = buffer.Slice(0, SnapshotMetadata.Size);
                metadata.Serialize(buffer.Span);
                return output.WriteAsync(buffer, token);
            }

            private async ValueTask WriteAsync<TEntry>(TEntry entry, long index, Memory<byte> buffer, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
            {
                Index = index;
                Position = SnapshotMetadata.Size;
                await entry.WriteToAsync(this, buffer, token).ConfigureAwait(false);
                var metadata = SnapshotMetadata.Create(entry, index, Length - SnapshotMetadata.Size);
                Position = 0;
                await WriteMetadataAsync(this, metadata, buffer, token).ConfigureAwait(false);
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index, CancellationToken token = default)
                where TEntry : notnull, IRaftLogEntry
                => WriteAsync(entry, index, session.Buffer, token);

            private static async ValueTask<LogEntry> ReadAsync(StreamSegment reader, Memory<byte> buffer, CancellationToken token)
            {
                reader.BaseStream.Position = 0;

                // snapshot reader stream may be out of sync with writer stream
                await reader.FlushAsync(token).ConfigureAwait(false);
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

        /// <summary>
        /// Indicates that the log record cannot be restored from the partition.
        /// </summary>
        public sealed class MissingLogEntryException : IntegrityException
        {
            internal MissingLogEntryException(long relativeIndex, long firstIndex, long lastIndex, string fileName)
                : base(ExceptionMessages.MissingLogEntry(relativeIndex + firstIndex, fileName))
            {
                Index = relativeIndex + firstIndex;
                PartitionFirstIndex = firstIndex;
                PartitionLastIndex = lastIndex;
                PartitionFileName = fileName;
            }

            /// <summary>
            /// Gets index of the log record.
            /// </summary>
            public long Index { get; }

            /// <summary>
            /// Gets index of the first log record in the partition.
            /// </summary>
            public long PartitionFirstIndex { get; }

            /// <summary>
            /// Gets index of the last log record in the partition.
            /// </summary>
            public long PartitionLastIndex { get; }

            /// <summary>
            /// Gets file name of the partition.
            /// </summary>
            public string PartitionFileName { get; }
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

        private Task GetOrCreatePartitionAsync(long recordIndex, [NotNull]ref Partition? partition)
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
        private bool TryGetPartition(long recordIndex, [NotNullWhen(true)]ref Partition? partition)
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
            var fileName = partition.FileName;
            await partition.DisposeAsync().ConfigureAwait(false);
            File.Delete(fileName);
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
