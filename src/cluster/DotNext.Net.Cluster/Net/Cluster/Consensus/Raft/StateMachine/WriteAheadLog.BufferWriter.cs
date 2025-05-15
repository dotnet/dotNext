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
        public required ulong LastWrittenAddress;

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

            var block = GetPage(out var offset).Memory.Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }

        public Span<byte> GetSpan(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            var block = GetPage(out var offset).GetSpan().Slice(offset);
            return sizeHint is 0 || sizeHint <= block.Length
                ? block
                : throw new InsufficientMemoryException();
        }
        
        private Page GetPage(out int offset)
            => GetOrAdd(GetPageIndex(LastWrittenAddress, out offset));
    }
}