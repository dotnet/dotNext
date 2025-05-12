using System.Buffers;
using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public partial class PersistentState
{
    private readonly PagedBufferWriter dataPages;
    
    private sealed class PagedBufferWriter(DirectoryInfo location, int pageSize) : PageManager(location, pageSize), IBufferWriter<byte>
    {
        public ulong LastWrittenAddress;

        public bool TryEnsureCapacity(long? length)
        {
            if (length is not { } len || len > PageSize)
                return false;

            EnsureCapacity((int)len);
            return true;
        }

        private void EnsureCapacity(int length)
        {
            Debug.Assert(length <= PageSize);

            GetPageIndex(LastWrittenAddress, out var offset);

            var remainingSpace = PageSize - offset;
            if (remainingSpace < length)
                LastWrittenAddress += (uint)remainingSpace;
        }

        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            LastWrittenAddress += (uint)count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            if (sizeHint <= PageSize)
            {
                var pageIndex = GetPageIndex(LastWrittenAddress, out var offset);
                var page = GetOrAdd(pageIndex);

                var block = page.Memory.Slice(offset);
                if (sizeHint is 0 || sizeHint <= block.Length)
                    return block;
            }

            throw new InsufficientMemoryException();
        }

        public Span<byte> GetSpan(int sizeHint) => GetMemory(sizeHint).Span;
    }
}