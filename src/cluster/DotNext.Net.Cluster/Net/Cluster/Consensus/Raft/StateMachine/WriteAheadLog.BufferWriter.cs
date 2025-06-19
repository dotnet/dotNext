using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly PagedBufferWriter dataPages;
    
    private sealed class PagedBufferWriter(PageManager manager) : Disposable, IBufferWriter<byte>, IMemoryView
    {
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
            buffer.CopyTo(page.Slice(offset), out var bytesWritten);
            buffer = buffer.Slice(bytesWritten);

            while (!buffer.IsEmpty)
            {
                page = manager.GetOrAddPage(++pageIndex).GetSpan();
                buffer.CopyTo(page, out bytesWritten);
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

        private MemoryManager<byte> GetOrAdd(out int offset)
            => manager.GetOrAddPage(manager.GetPageIndex(LastWrittenAddress, out offset));

        ReadOnlySequence<byte> IMemoryView.GetSequence(ulong address, long length)
            => manager.GetRange(address, length);

        bool IMemoryView.TryGetMemory(ulong address, long length, out ReadOnlyMemory<byte> memory)
            => manager.GetRange(address, length).TryGetMemory(out memory);

        IEnumerable<ReadOnlyMemory<byte>> IMemoryView.EnumerateMemoryBlocks(ulong address, long length)
            => manager.GetRange(address, length);
        
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