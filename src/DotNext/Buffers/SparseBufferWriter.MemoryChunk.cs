namespace DotNext.Buffers;

public partial class SparseBufferWriter<T>
{
    internal abstract class MemoryChunk : Disposable
    {
        private protected MemoryChunk(MemoryChunk? previous)
        {
            if (previous is not null)
                previous.Next = this;
        }

        internal abstract int FreeCapacity { get; }

        internal abstract Memory<T> FreeMemory { get; }

        internal abstract ReadOnlyMemory<T> WrittenMemory { get; }

        internal MemoryChunk? Next
        {
            get;
            private set;
        }

        internal abstract int Write(ReadOnlySpan<T> input);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Next = null;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ImportedMemoryChunk : MemoryChunk
    {
        internal ImportedMemoryChunk(ReadOnlyMemory<T> memory, MemoryChunk? previous = null)
            : base(previous) => WrittenMemory = memory;

        internal override ReadOnlyMemory<T> WrittenMemory { get; }

        internal override int FreeCapacity => 0;

        internal override Memory<T> FreeMemory => Memory<T>.Empty;

        internal override int Write(ReadOnlySpan<T> input) => 0;
    }

    private sealed class PooledMemoryChunk : MemoryChunk
    {
        private MemoryOwner<T> owner;
        private int writtenCount;

        internal PooledMemoryChunk(MemoryAllocator<T>? allocator, int length, MemoryChunk? previous = null)
            : base(previous)
            => owner = allocator.Invoke(length, exactSize: false);

        /// <summary>
        /// Indicates that the chunk has no occupied elements in the memory.
        /// </summary>
        internal bool IsUnused => writtenCount is 0;

        internal override Memory<T> FreeMemory => owner.Memory.Slice(writtenCount);

        internal override int FreeCapacity => owner.Length - writtenCount;

        internal override ReadOnlyMemory<T> WrittenMemory => owner.Memory.Slice(0, writtenCount);

        internal override int Write(ReadOnlySpan<T> input)
        {
            input.CopyTo(FreeMemory.Span, out var count);
            writtenCount += count;
            return count;
        }

        internal void Advance(int count)
        {
            var length = writtenCount + count;
            if ((uint)length > (uint)owner.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            writtenCount = length;
        }

        internal void Realloc(MemoryAllocator<T>? allocator, int length)
        {
            owner.Dispose();
            owner = allocator.Invoke(length, exactSize: false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                owner.Dispose();
            }

            writtenCount = 0;
            base.Dispose(disposing);
        }
    }
}