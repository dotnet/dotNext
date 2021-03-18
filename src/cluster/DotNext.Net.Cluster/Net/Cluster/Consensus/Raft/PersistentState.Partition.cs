using System;
using System.Collections.Generic;
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
            internal readonly long FirstIndex;
            internal readonly int Capacity;    // max number of entries
            private readonly MemoryOwner<LogEntryMetadata> lookupCache;

            internal Partition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, MemoryAllocator<LogEntryMetadata>? cachePool, int readersCount, bool writeThrough)
                : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize, readersCount, GetOptions(writeThrough))
            {
                Capacity = recordsPerPartition;
                FirstIndex = partitionNumber * recordsPerPartition;
                lookupCache = cachePool is null ? default : cachePool(recordsPerPartition);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static FileOptions GetOptions(bool writeThrough)
            {
                const FileOptions skipBufferOptions = FileOptions.RandomAccess | FileOptions.WriteThrough | FileOptions.Asynchronous;
                const FileOptions dontSkipBufferOptions = FileOptions.RandomAccess | FileOptions.Asynchronous;
                return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
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

            private async ValueTask<LogEntry> ReadAsync(StreamSegment reader, Memory<byte> buffer, int index, bool refreshStream, CancellationToken token)
            {
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");

                // find pointer to the content
                LogEntryMetadata metadata;
                if (refreshStream)
                    await reader.FlushAsync(token).ConfigureAwait(false);
                if (lookupCache.IsEmpty)
                {
                    reader.BaseStream.Position = index * LogEntryMetadata.Size;
                    metadata = await ReadMetadataAsync(reader.BaseStream, buffer, token).ConfigureAwait(false);
                }
                else
                {
                    metadata = lookupCache[index];
                }

                return metadata.Offset > 0 ? new LogEntry(reader, buffer, metadata) : throw new MissingLogEntryException(index, FirstIndex, LastIndex, FileName);
            }

            internal ValueTask<LogEntry> ReadAsync(in DataAccessSession session, long index, bool absoluteIndex, bool refreshStream, CancellationToken token)
            {
                // calculate relative index
                if (absoluteIndex)
                    index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                return ReadAsync(GetReadSessionStream(session), session.Buffer, (int)index, refreshStream, token);
            }

            private async ValueTask WriteAsync<TEntry>(TEntry entry, int index, Memory<byte> buffer)
                where TEntry : notnull, IRaftLogEntry
            {
                // calculate offset of the previous entry
                long offset;
                LogEntryMetadata metadata;
                if (index == 0L || index == 1L && FirstIndex == 0L)
                {
                    offset = PayloadOffset;
                }
                else if (lookupCache.IsEmpty)
                {
                    // read content offset and the length of the previous entry
                    Position = (index - 1) * LogEntryMetadata.Size;
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
                Position = index * LogEntryMetadata.Size;
                await WriteMetadataAsync(this, metadata, buffer).ConfigureAwait(false);

                // update cache
                if (!lookupCache.IsEmpty)
                    lookupCache[index] = metadata;
            }

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index)
                where TEntry : notnull, IRaftLogEntry
            {
                // calculate relative index
                index -= FirstIndex;
                Debug.Assert(index >= 0 && index < Capacity, $"Invalid index value {index}, offset {FirstIndex}");
                return WriteAsync(entry, (int)index, session.Buffer);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    lookupCache.Dispose();
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

            internal ValueTask WriteAsync<TEntry>(in DataAccessSession session, TEntry entry, long index, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
                => WriteAsync(entry, index, session.Buffer, token);

            private static async ValueTask<LogEntry> ReadAsync(StreamSegment reader, Memory<byte> buffer, CancellationToken token)
            {
                reader.BaseStream.Position = 0;

                // snapshot reader stream may be out of sync with writer stream
                await reader.FlushAsync(token).ConfigureAwait(false);
                return new LogEntry(reader, buffer, await ReadMetadataAsync(reader.BaseStream, buffer, token).ConfigureAwait(false));
            }

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

        // key is the number of partition
        private readonly IDictionary<long, Partition> partitionTable;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long PartitionOf(long recordIndex) => recordIndex / recordsPerPartition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetPartition(long recordIndex, [NotNullWhen(true)]ref Partition? partition)
            => partition is not null && recordIndex >= partition.FirstIndex && recordIndex <= partition.LastIndex || partitionTable.TryGetValue(PartitionOf(recordIndex), out partition);

        private bool TryGetPartition(long recordIndex, [NotNullWhen(true)]ref Partition? partition, out bool switched)
        {
            var previous = partition;
            var result = TryGetPartition(recordIndex, ref partition);
            switched = partition != previous;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Task FlushAsync(Partition? partition) => partition is null ? Task.CompletedTask : partition.FlushAsync();

        private void GetOrCreatePartition(long recordIndex, [NotNull] out Partition? partition)
        {
            var partitionNumber = PartitionOf(recordIndex);
            if (!partitionTable.TryGetValue(partitionNumber, out partition))
            {
                partition = CreatePartition(partitionNumber);
                partition.Allocate(initialSize);
                partitionTable.Add(partitionNumber, partition);
            }
        }

        private Task GetOrCreatePartitionAsync(long recordIndex, [NotNull]ref Partition? partition)
        {
            Task flushTask;

            if (partition is null || recordIndex < partition.FirstIndex || recordIndex > partition.LastIndex)
            {
                flushTask = FlushAsync(partition);
                GetOrCreatePartition(recordIndex, out partition);
            }
            else
            {
                flushTask = Task.CompletedTask;
            }

            return flushTask;
        }

        private void RemovePartitions(IDictionary<long, Partition> partitions)
        {
            foreach (var (partitionNumber, partition) in partitions)
            {
                partitionTable.Remove(partitionNumber);
                var fileName = partition.FileName;
                partition.Dispose();
                File.Delete(fileName);
            }
        }
    }
}
