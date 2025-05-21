using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly PagedBufferWriter dataPages;

    private sealed class PagedBufferWriter(DirectoryInfo location, int pageSize) : PageManager(location, pageSize), IBufferWriter<byte>, IMemoryView
    {
        public required ulong LastWrittenAddress;

        public bool TryEnsureCapacity(long? length)
        {
            if (length is not { } len || len > (uint)PageSize)
                return false;

            var remainingSpace = PageSize - GetPageOffset(LastWrittenAddress, PageSize);
            if (remainingSpace < len)
            {
                LastWrittenAddress += (uint)remainingSpace;
            }

            return true;
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            var length = (uint)buffer.Length;
            var pageIndex = GetPageIndex(LastWrittenAddress, out var offset);
            var page = GetOrAdd(pageIndex).GetSpan();
            buffer.CopyTo(page.Slice(offset), out var bytesWritten);
            buffer = buffer.Slice(bytesWritten);

            while (!buffer.IsEmpty)
            {
                page = GetOrAdd(++pageIndex).GetSpan();
                buffer.CopyTo(page, out bytesWritten);
                buffer = buffer.Slice(bytesWritten);
            }

            LastWrittenAddress += length;
        }

        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            LastWrittenAddress += (uint)count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var block = GetOrAdd(out var offset).Memory.Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }

        public Span<byte> GetSpan(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var block = GetOrAdd(out var offset).GetSpan().Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }

        private Page GetOrAdd(out int offset)
            => GetOrAdd(GetPageIndex(LastWrittenAddress, out offset));

        ReadOnlySequence<byte> IMemoryView.GetSequence(ulong address, long length)
            => GetRange(address, length);

        bool IMemoryView.TryGetMemory(ulong address, long length, out ReadOnlyMemory<byte> memory)
            => GetRange(address, length).TryGetMemory(out memory);

        IEnumerable<ReadOnlyMemory<byte>> IMemoryView.EnumerateMemoryBlocks(ulong address, long length)
            => GetRange(address, length);
    }
}