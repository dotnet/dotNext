using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

public partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly PagedBufferWriter dataPages;
    
    private sealed class PagedBufferWriter(PageManager manager) : Disposable, IBufferWriter<byte>, IMemoryView, IAsyncBinaryWriter
    {
        public const string LocationPrefix = "data";
        
        internal required ulong LastWrittenAddress;

        public long DeletePages(ulong toAddress)
        {
            var toPage = manager.GetPageIndex(toAddress, out _);
            return manager.DeletePages(toPage) * (long)manager.PageSize;
        }

        public bool TryEnsureCapacity(long? length)
        {
            if (length is not { } len || len > (uint)manager.PageSize)
                return false;

            var remainingSpace = manager.PageSize - GetPageOffset(LastWrittenAddress, manager.PageSize);
            if (remainingSpace < len)
            {
                LastWrittenAddress += (uint)remainingSpace;
            }

            return true;
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            var length = (uint)buffer.Length;
            var pageIndex = manager.GetPageIndex(LastWrittenAddress, out var offset);
            var page = manager.GetOrAddPage(pageIndex).GetSpan();
            var bytesWritten = buffer >>> page.Slice(offset);
            buffer = buffer.Slice(bytesWritten);

            while (!buffer.IsEmpty)
            {
                page = manager.GetOrAddPage(++pageIndex).GetSpan();
                bytesWritten = buffer >>> page;
                buffer = buffer.Slice(bytesWritten);
            }

            LastWrittenAddress += length;
        }
        
        public ValueTask FlushAsync(ulong startAddress, ulong endAddress, CancellationToken token)
        {
            var startPage = manager.GetPageIndex(startAddress, out var startOffset);
            var endPage = manager.GetPageIndex(endAddress, out var endOffset);
            return manager.FlushAsync(startPage, startOffset, endPage, endOffset, token);
        }
        
        void IBufferWriter<byte>.Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            LastWrittenAddress += (uint)count;
        }

        ValueTask IAsyncBinaryWriter.AdvanceAsync(int count, CancellationToken token)
        {
            ValueTask task;
            if (count < 0)
            {
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(count)));
            }
            else
            {
                task = ValueTask.CompletedTask;
                LastWrittenAddress += (uint)count;
            }

            return task;
        }

        Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var block = GetOrAdd(out var offset).Memory.Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }

        Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var block = GetOrAdd(out var offset).GetSpan().Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }

        Memory<byte> IAsyncBinaryWriter.Buffer => GetOrAdd(out var offset).Memory.Slice(offset);

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> source, CancellationToken token)
        {
            var task = ValueTask.CompletedTask;
            try
            {
                Write(source.Span);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }

            return task;
        }

        private MemoryManager<byte> GetOrAdd(out int offset)
            => manager.GetOrAddPage(manager.GetPageIndex(LastWrittenAddress, out offset));

        ReadOnlySequence<byte> IMemoryView.GetSequence(ulong address, long length)
            => manager.GetRange(address, length);

        bool IMemoryView.TryGetMemory(ulong address, long length, out ReadOnlyMemory<byte> memory)
            => manager.GetRange(address, length).TryGetMemory(out memory);

        IEnumerable<ReadOnlyMemory<byte>> IMemoryView.EnumerateMemoryBlocks(ulong address, long length)
            => manager.GetRange(address, length);

        public void ComputeHash(NonCryptographicHashAlgorithm hash, ulong offset, long length)
        {
            // hash data
            foreach (var fragment in manager.GetRange(offset, length))
            {
                hash.Append(fragment.Span);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                manager.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}