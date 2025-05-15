using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

public partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly PagedBufferWriter dataPages;
    
    private sealed class PagedBufferWriter(DirectoryInfo location, int pageSize) : PageManager(location, pageSize), IBufferWriter<byte>
    {
        public ulong LastWrittenAddress;

        public int? TryEnsureCapacity(long? length)
        {
            if (length is not { } len || (ulong)len > (uint)PageSize)
                return null;

            var result = (int)len;
            EnsureCapacity(result);
            return result;
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

            var page = GetOrAdd(GetPageIndex(LastWrittenAddress, out var offset));

            var block = page.Memory.Slice(offset);
            if (sizeHint is 0 || sizeHint <= block.Length)
                return block;

            throw new InsufficientMemoryException();
        }

        public Span<byte> GetSpan(int sizeHint) => GetMemory(sizeHint).Span;
    }
}