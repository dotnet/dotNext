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
        private static readonly CacheRecord EmptyRecord = new() { PersistenceMode = CachedLogEntryPersistenceMode.CopyToBuffer };

        internal readonly long FirstIndex, PartitionNumber, LastIndex;
        private Partition? previous, next;
        private object?[]? context;
        private MemoryOwner<CacheRecord> entryCache;
        protected int runningIndex;

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

        internal void ClearContext(long absoluteIndex)
        {
            Debug.Assert(absoluteIndex >= FirstIndex);
            Debug.Assert(absoluteIndex <= LastIndex);

            if (context is not null)
            {
                var relativeIndex = ToRelativeIndex(absoluteIndex);
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(context), relativeIndex) = null;
            }
        }

        internal void ClearContext() => context = null;

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
                    Context = GetContext(relativeIndex),
                };
            }

        return_cached:
            return new(in metadata, absoluteIndex)
            {
                ContentBuffer = cachedContent.Content.Memory,
                IsPersisted = cachedContent.PersistenceMode is not CachedLogEntryPersistenceMode.None,
                Context = cachedContent.Context,
            };

            object? GetContext(int index)
            {
                Debug.Assert(index <= ToRelativeIndex(LastIndex));

                return context is not null
                    ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(context), index)
                    : null;
            }
        }

        internal ValueTask PersistCachedEntryAsync(long absoluteIndex, bool removeFromMemory)
        {
            Debug.Assert(entryCache.IsEmpty is false);

            var index = ToRelativeIndex(absoluteIndex);
            Debug.Assert((uint)index < (uint)entryCache.Length);

            ref var cachedEntry = ref entryCache[index];
            Debug.Assert(cachedEntry.PersistenceMode is CachedLogEntryPersistenceMode.None);
            cachedEntry.PersistenceMode = CachedLogEntryPersistenceMode.CopyToBuffer;
            var offset = GetMetadata(index).Offset;

            return cachedEntry.Content.IsEmpty
                ? ValueTask.CompletedTask
                : removeFromMemory
                ? PersistAndDeleteAsync(cachedEntry.Content.Memory, index, offset)
                : PersistAsync(cachedEntry.Content.Memory, index, offset);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask PersistAsync(ReadOnlyMemory<byte> content, int index, long offset)
        {
            await SetWritePositionAsync(offset).ConfigureAwait(false);
            await writer.WriteAsync(content).ConfigureAwait(false);

            runningIndex = index;
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

            runningIndex = index;
        }

        internal long GetTerm(long absoluteIndex)
        {
            Debug.Assert(absoluteIndex >= FirstIndex && absoluteIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            return GetMetadata(ToRelativeIndex(absoluteIndex)).Term;
        }

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
                    case CachedLogEntryPersistenceMode.SkipBuffer when !writer.HasBufferedData:
                        return WriteThroughAsync(cachedEntry, relativeIndex, token);
                    case CachedLogEntryPersistenceMode.None:
                        OnCached(in cachedEntry, relativeIndex);
                        return ValueTask.CompletedTask;
                    default:
                        goto exit;
                }
            }
            else if (entry is IInputLogEntry && ((IInputLogEntry)entry).Context is { } context)
            {
                SetContext(relativeIndex, context);
            }

            // invalidate cached log entry on write
            if (!entryCache.IsEmpty)
                entryCache[relativeIndex].Dispose();

            exit:
            return PersistAsync(entry, relativeIndex, token);

            void SetContext(int relativeIndex, object context)
            {
                Debug.Assert(context is not null);

                this.context ??= new object?[ToRelativeIndex(LastIndex) + 1];
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(this.context), relativeIndex) = context;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                previous = next = null;

                if (context is not null)
                {
                    Array.Clear(context);
                    context = null;
                }

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
            Debug.Assert(writer.HasBufferedData is false);

            if (entry.Length > maxLogEntrySize)
                throw new InvalidOperationException(ExceptionMessages.LogEntryPayloadTooLarge);

            var metadata = LogEntryMetadata.Create(entry, GetMetadataOffset(index) + LogEntryMetadata.Size);
            metadata.Format(metadataBuffer.Span);

            payloadBuffer = entry.Content.Memory;
            await RandomAccess.WriteAsync(Handle, this, GetMetadataOffset(index), token).ConfigureAwait(false);
            payloadBuffer = default;
            metadataTable[index].Metadata = metadata;

            writer.FilePosition = metadata.End;
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
        private MemoryOwner<byte> header, footer;
        private (ReadOnlyMemory<byte>, ReadOnlyMemory<byte>) bufferTuple;

        internal Table(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
            : base(location, HeaderSize, bufferSize, recordsPerPartition, partitionNumber, in manager, readersCount, writeMode, initialSize)
        {
            footer = manager.BufferAllocator.AllocateExactly(recordsPerPartition * LogEntryMetadata.Size);
#if DEBUG
            footer.Span.Clear();
#endif

            header = manager.BufferAllocator.AllocateExactly(HeaderSize);
            header.Span.Clear();

            // init ephemeral 0 entry
            if (PartitionNumber is 0L)
            {
                LogEntryMetadata.Create(LogEntry.Initial, HeaderSize + LogEntryMetadata.Size, length: 0L).Format(footer.Span);
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

            long fileOffset;

            // read header
            if (RandomAccess.Read(Handle, header.Span, fileOffset: 0L) < HeaderSize)
            {
                header.Span.Clear();
                writer.FilePosition = HeaderSize;
            }
            else if (IsSealed)
            {
                // partition is completed, read table
                writer.FilePosition = fileOffset = RandomAccess.GetLength(Handle);

                if (fileOffset < footer.Length + HeaderSize)
                    throw new IntegrityException(ExceptionMessages.InvalidPartitionFormat);

                fileOffset -= footer.Length;
                RandomAccess.Read(Handle, footer.Span, fileOffset);
                runningIndex = int.CreateChecked(LastIndex - FirstIndex);
            }
            else
            {
                // read sequentially every log entry
                int footerOffset;

                if (PartitionNumber is 0L)
                {
                    footerOffset = LogEntryMetadata.Size;
                    fileOffset = HeaderSize + LogEntryMetadata.Size;
                }
                else
                {
                    footerOffset = 0;
                    fileOffset = HeaderSize;
                }

                for (Span<byte> metadataBuffer = stackalloc byte[LogEntryMetadata.Size], metadataTable = footer.Span;
                     footerOffset < footer.Length;
                     footerOffset += LogEntryMetadata.Size, runningIndex++)
                {
                    var count = RandomAccess.Read(Handle, metadataBuffer, fileOffset);
                    if (count < LogEntryMetadata.Size)
                        break;

                    fileOffset = LogEntryMetadata.GetEndOfLogEntry(metadataBuffer);
                    if (fileOffset <= 0L)
                        break;

                    writer.FilePosition = fileOffset;
                    metadataBuffer.CopyTo(metadataTable.Slice(footerOffset, LogEntryMetadata.Size));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Memory<byte> GetMetadataBuffer(int index)
            => footer.Memory.Slice(index * LogEntryMetadata.Size, LogEntryMetadata.Size);

        protected override LogEntryMetadata GetMetadata(int index)
            => new(GetMetadataBuffer(index).Span);

        private long GetWriteAddress(int index)
            => index is 0 ? fileOffset : LogEntryMetadata.GetEndOfLogEntry(GetMetadataBuffer(index - 1).Span);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask PersistAsync<TEntry>(TEntry entry, int index, CancellationToken token)
        {
            var writeAddress = GetWriteAddress(index);
            await UnsealIfNeededAsync(writeAddress, token).ConfigureAwait(false);

            LogEntryMetadata metadata;
            var metadataBuffer = GetMetadataBuffer(index);
            var startPos = writeAddress + LogEntryMetadata.Size;
            if (entry.Length is { } length)
            {
                // fast path - write metadata and entry sequentially
                metadata = LogEntryMetadata.Create(entry, startPos, length);

                await SetWritePositionAsync(writeAddress, token).ConfigureAwait(false);
                await writer.WriteAsync(metadata, token).ConfigureAwait(false);
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);

                metadata.Format(metadataBuffer.Span);
            }
            else
            {
                // slow path - write entry first
                await SetWritePositionAsync(startPos, token).ConfigureAwait(false);

                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                length = writer.WritePosition - startPos;

                metadata = LogEntryMetadata.Create(entry, startPos, length);
                metadata.Format(metadataBuffer.Span);
                await RandomAccess.WriteAsync(Handle, metadataBuffer, writeAddress, token).ConfigureAwait(false);
            }

            runningIndex = index;
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        protected override async ValueTask WriteThroughAsync(CachedLogEntry entry, int index, CancellationToken token)
        {
            Debug.Assert(writer.HasBufferedData is false);

            var writeAddress = GetWriteAddress(index);
            await UnsealIfNeededAsync(writeAddress, token).ConfigureAwait(false);

            var startPos = writeAddress + LogEntryMetadata.Size;
            var metadata = LogEntryMetadata.Create(entry, startPos, entry.Length);
            var metadataBuffer = GetMetadataBuffer(index);
            metadata.Format(metadataBuffer.Span);

            bufferTuple = (metadataBuffer, entry.Content.Memory);
            await RandomAccess.WriteAsync(Handle, this, writeAddress, token).ConfigureAwait(false);
            bufferTuple = default;

            runningIndex = index;
            writer.FilePosition = metadata.End;
        }

        protected override void OnCached(in CachedLogEntry cachedEntry, int index)
        {
            var startPos = GetWriteAddress(index) + LogEntryMetadata.Size;
            var metadata = LogEntryMetadata.Create(in cachedEntry, startPos);
            metadata.Format(GetMetadataBuffer(index).Span);
        }

        private ValueTask UnsealIfNeededAsync(long truncatePosition, CancellationToken token)
        {
            ValueTask task;
            if (IsSealed)
            {
                task = UnsealAsync(truncatePosition, token);
            }
            else if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else if (truncatePosition < writer.FilePosition)
            {
                task = new();
                try
                {
                    // The caller is trying to rewrite the log entry.
                    // For a correctness of Initialize() method for unsealed partitions, we
                    // need to adjust file size. This is expensive syscall which can lead to file fragmentation.
                    // However, this is acceptable because rare.
                    RandomAccess.SetLength(Handle, truncatePosition);
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }
            else
            {
                task = new();
            }

            return task;
        }

        private async ValueTask UnsealAsync(long truncatePosition, CancellationToken token)
        {
            // This is expensive operation in terms of I/O. However, it is needed only when
            // the consumer decided to rewrite the existing log entry, which is rare.
            IsSealed = false;
            await WriteHeaderAsync(token).ConfigureAwait(false);
            RandomAccess.FlushToDisk(Handle);

            // destroy all entries in the tail of partition
            RandomAccess.SetLength(Handle, truncatePosition);
        }

        public override ValueTask FlushAsync(CancellationToken token = default)
        {
            return IsSealed
                ? ValueTask.CompletedTask
                : runningIndex == LastIndex
                ? FlushAndSealAsync(token)
                : base.FlushAsync(token);
        }

        private async ValueTask FlushAndSealAsync(CancellationToken token)
        {
            Debug.Assert(writer.FilePosition > HeaderSize);
            
            Invalidate();
            
            // use scatter I/O to flush the rest of the partition
            if (writer.HasBufferedData)
            {
                bufferTuple = (writer.WrittenBuffer, footer.Memory);
                await RandomAccess.WriteAsync(Handle, this, writer.FilePosition, token).ConfigureAwait(false);
                writer.ClearBuffer();
                writer.FilePosition += bufferTuple.Item1.Length;
                bufferTuple = default;
            }
            else
            {
                await RandomAccess.WriteAsync(Handle, footer.Memory, writer.FilePosition, token).ConfigureAwait(false);
            }

            RandomAccess.FlushToDisk(Handle);
            RandomAccess.SetLength(Handle, writer.FilePosition + footer.Length);

            IsSealed = true;
            await WriteHeaderAsync(token).ConfigureAwait(false);

            RandomAccess.FlushToDisk(Handle);
        }

        private ValueTask WriteHeaderAsync(CancellationToken token)
            => RandomAccess.WriteAsync(Handle, header.Memory, fileOffset: 0L, token);

        ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index] => index switch
        {
            0 => bufferTuple.Item1,
            1 => bufferTuple.Item2,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

        private IEnumerator<ReadOnlyMemory<byte>> GetEnumerator()
        {
            yield return bufferTuple.Item1;
            yield return bufferTuple.Item2;
        }

        IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            header.Dispose();
            footer.Dispose();
            base.Dispose(disposing);
        }
    }

    /*
       Partition file format:
       FileName - number of partition
       Payload:
       [struct LogEntryMetadata] X Capacity - prologue with metadata
       [octet string] X number of entries
    */
    [Obsolete]
    private sealed class LegacyPartition : Partition
    {
        // metadata management
        private MemoryOwner<byte> metadata;
        private int metadataFlushStartAddress;
        private int metadataFlushEndAddress;

        // represents offset within the file from which a newly added log entry payload can be recorded
        private long writeAddress;

        internal LegacyPartition(DirectoryInfo location, int bufferSize, int recordsPerPartition, long partitionNumber, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize)
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
            await RandomAccess.WriteAsync(Handle, entry.Content.Memory, writeAddress, token).ConfigureAwait(false);

            // save new log entry to the allocation table
            WriteMetadata(index, LogEntryMetadata.Create(entry, writeAddress, entry.Length));
            writeAddress += entry.Length;
        }

        protected override void OnCached(in CachedLogEntry cachedEntry, int index)
        {
            WriteMetadata(index, LogEntryMetadata.Create(cachedEntry, writeAddress, cachedEntry.Length));
            writeAddress += cachedEntry.Length;
        }

        protected override void Dispose(bool disposing)
        {
            metadata.Dispose();
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
            return;
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
                        return;
                    }

                    // nothing on the right side, create new tail
                    if (partition.IsLast)
                    {
                        LastPartition = partition = Append(partitionNumber, partition);
                        return;
                    }

                    partition = partition.Next;
                    break;
                case < 0:
                    if (previous > 0)
                    {
                        partition = Prepend(partitionNumber, partition);
                        return;
                    }

                    // nothing on the left side, create new head
                    if (partition.IsFirst)
                    {
                        FirstPartition = partition = Prepend(partitionNumber, partition);
                        return;
                    }

                    partition = partition.Previous;
                    break;
                default:
                    return;
            }

            Debug.Assert(partition is not null);
        }

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
