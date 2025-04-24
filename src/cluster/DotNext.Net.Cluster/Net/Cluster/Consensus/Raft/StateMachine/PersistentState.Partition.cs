using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using IO;
using IntegrityException = IO.Log.IntegrityException;

public partial class PersistentState
{
    // Partition format:
    // Header - 512 bytes
    // Allocation table - sector size - 512 bytes. For 4K, the partition can have maximum: 4096 - 512 / LogEntryMetadata.Size = 89 log entries
    // [LogEntryMetadata.Size] [payload] X N - log entries
    // 
    // Header + allocation table is a sector size, that allows to write a partition prologue atomically to the disk
    private sealed class Partition : FileWriter
    {
        private const int PrologueSize = 4096;
        
        // Header format:
        // 1 byte - sealed or not
        private const int HeaderSize = 512;

        internal const int MaxRecordsPerPartition = (PrologueSize - HeaderSize) / LogEntryMetadata.Size;

        internal readonly long FirstIndex, PartitionNumber, LastIndex;

        private Partition? previous, next;
        private object?[]? context;
        
        // cache management
        private ulong cacheReferenceCounter;
        private MemoryOwner<MemoryOwner<byte>> cachedEntries;

        internal Partition(DirectoryInfo directory, int recordsPerPartition, long partitionNumber, long initialSize, in BufferManager manager)
            : base(OpenHandle(directory, partitionNumber, initialSize, out var created))
        {
            PartitionNumber = partitionNumber;
            FirstIndex = partitionNumber * recordsPerPartition;
            LastIndex = FirstIndex + recordsPerPartition - 1L;
            Allocator = manager.BufferAllocator;

            File.SetAttributes(handle, FileAttributes.NotContentIndexed);
            
            header = Allocator.AllocateExactly(HeaderSize);
            footer = Allocator.AllocateExactly(checked(recordsPerPartition * LogEntryMetadata.Size + sizeof(long)));

            if (manager.IsCachingEnabled)
            {
                cacheReferenceCounter = 1UL;
                cachedEntries = manager.CacheAllocator.AllocateExactly(recordsPerPartition);
            }

            long fileOffset;
            if (created || RandomAccess.Read(handle, header.Span, fileOffset: 0L) < HeaderSize || !IsSealed)
            {
                header.Span.Clear();
                RandomAccess.Write(handle, header.Span, 0L);
                RandomAccess.FlushToDisk(handle); // release memory page associated with the header
                
                InitializeFooter(footer.Span, recordsPerPartition);
                fileOffset = HeaderSize;
            }
            else
            {
                fileOffset = RandomAccess.GetLength(handle);

                if (FilePosition < footer.Length + HeaderSize)
                    throw new IntegrityException(ExceptionMessages.InvalidPartitionFormat);

                fileOffset -= footer.Length;

                // read footer
                RandomAccess.Read(handle, footer.Span, fileOffset);
            }

            FilePosition = fileOffset;
            
            static void InitializeFooter(Span<byte> footer, int recordsPerPartition)
            {
                for (var index = 0; index < recordsPerPartition; index++)
                {
                    var writeAddress = index * LogEntryMetadata.Size;
                    var metadata = new LogEntryMetadata(default, 0L, writeAddress + HeaderSize + LogEntryMetadata.Size, 0L);
                    metadata.Format(footer.Slice(writeAddress));
                }
                
                footer[..^sizeof(long)].Clear();
            }
        }

        private static SafeFileHandle OpenHandle(DirectoryInfo directory, long partitionNumber, long initialSize, out bool created)
        {
            var fileName = Path.Combine(directory.FullName, partitionNumber.ToString(InvariantCulture));

            FileMode fileMode;
            if (File.Exists(fileName))
            {
                fileMode = FileMode.OpenOrCreate;
                initialSize = 0L;
                created = false;
            }
            else
            {
                fileMode = FileMode.CreateNew;
                created = true;
                initialSize = checked(initialSize + HeaderSize);
            }

            return File.OpenHandle(fileName, fileMode, FileAccess.ReadWrite, FileShare.Read, FileOptions.None, initialSize);
        }
        
        private bool IsSealed
        {
            get => Unsafe.BitCast<byte, bool>(MemoryMarshal.GetReference(header.Span));
            set => MemoryMarshal.GetReference(header.Span) = Unsafe.BitCast<bool, byte>(value);
        }

        internal long CommitIndex
        {
            get => BinaryPrimitives.ReadInt64LittleEndian(footer.Span[..^sizeof(long)]);
            set => BinaryPrimitives.WriteInt64LittleEndian(footer.Span[..^sizeof(long)], value);
        }

        internal void FlushAndSeal()
        {
            Write(footer.Span);
            Write();
            FlushToDisk();
            RandomAccess.SetLength(handle, FilePosition);

            // header write is atomic, since the header size is less than the system memory page
            IsSealed = true;
            RandomAccess.Write(handle, header.Span, fileOffset: 0L);
            RandomAccess.FlushToDisk(handle);
        }

        internal ValueTask WriteAsync<TEntry>(TEntry entry, long absoluteIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
        {
            if (IsSealed)
                Unseal();
            
            // write operation always expects absolute index, so we need to convert it to the relative index
            var relativeIndex = ToRelativeIndex(absoluteIndex);
            Debug.Assert(absoluteIndex >= FirstIndex && relativeIndex <= LastIndex, $"Invalid index value {absoluteIndex}, offset {FirstIndex}");

            if (entry is IInputLogEntry { Context: { } ctx })
            {
                context ??= new object?[ToRelativeIndex(LastIndex) + 1];
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(context), relativeIndex) = ctx;
            }
            
            var offset = GetWriteAddress(relativeIndex);
            FilePosition = offset;

            if (entry is ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)
            {
                Debug.Assert(Allocator is not null);

                var buffer = ((ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)entry).Invoke(Allocator);
                LogEntryMetadata.Create(entry, offset, WritePosition - offset).Format(GetMetadataBuffer(relativeIndex));
                
                Write(buffer.Span);
                Write(); // write to the system pages but without flush
                
                return ValueTask.CompletedTask;
            }

            return WriteAsync(entry, offset, relativeIndex, token);
        }

        private void Unseal()
        {
            // This is an expensive operation in terms of I/O. However, it's required only when
            // the consumer decided to rewrite the existing log entry, which is rare.
            IsSealed = false;
            RandomAccess.Write(handle, header.Span, fileOffset: 0L);
            RandomAccess.FlushToDisk(handle);
        }

        internal void ClearContext()
        {
            if (context is not null)
            {
                Array.Clear(context);
                context = null;
            }
        }

        private async ValueTask WriteAsync<TEntry>(TEntry entry, long offset, int relativeIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
        {
            await entry.WriteToAsync(this, token).ConfigureAwait(false);
            Write(); // write to the system pages but without flush

            LogEntryMetadata.Create(entry, offset, WritePosition - offset).Format(GetMetadataBuffer(relativeIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> GetMetadataBuffer(int index)
            => footer.Span.Slice(index * LogEntryMetadata.Size, LogEntryMetadata.Size);

        private long GetWriteAddress(int index)
            => index is 0 ? HeaderSize : LogEntryMetadata.GetEndOfLogEntry(GetMetadataBuffer(index - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ToRelativeIndex(long absoluteIndex)
            => unchecked((int)(absoluteIndex - FirstIndex));

        [MemberNotNullWhen(false, nameof(Previous))]
        internal bool IsFirst => previous is null;

        [MemberNotNullWhen(false, nameof(Next))]
        internal bool IsLast => next is null;

        internal Partition? Next => next;

        internal Partition? Previous => previous;

        internal bool Contains(long recordIndex)
            => recordIndex >= FirstIndex && recordIndex <= LastIndex;

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

        internal void TryUncache()
        {
            for (ulong current = Volatile.Read(in cacheReferenceCounter), tmp;; current = tmp)
            {
                if (current is 0UL)
                    break;

                tmp = Interlocked.CompareExchange(ref cacheReferenceCounter, current - 1UL, current);

                if (tmp != current)
                    continue;

                if (tmp is 1UL)
                {
                    cachedEntries.Dispose();
                }
                
                break;
            }
        }

        internal void KeepCacheAlive()
        {
            for (ulong current = Volatile.Read(in cacheReferenceCounter), tmp;; current = tmp)
            {
                if (current is 0UL)
                    break;

                tmp = Interlocked.CompareExchange(ref cacheReferenceCounter, current + 1UL, current);

                if (tmp == current)
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cachedEntries.Dispose();
                header.Dispose();
                footer.Dispose();
                handle.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}