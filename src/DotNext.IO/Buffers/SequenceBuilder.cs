using System;
using System.Buffers;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents builder of non-contiguous memory buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    public class SequenceBuilder<T> : SparseBufferWriter<T>, IReadOnlySequenceSource<T>
    {
        /// <summary>
        /// Initializes a new builder with the specified size of memory block.
        /// </summary>
        /// <param name="chunkSize">The size of the memory block representing single segment within sequence.</param>
        /// <param name="allocator">The allocator used to rent the segments.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is less than or equal to zero.</exception>
        public SequenceBuilder(int chunkSize, MemoryAllocator<T>? allocator = null)
            : base(chunkSize, allocator)
        {
        }

        /// <summary>
        /// Initializes a new builder with automatically selected
        /// chunk size.
        /// </summary>
        /// <param name="pool">Memory pool used to allocate memory chunks.</param>
        public SequenceBuilder(MemoryPool<T> pool)
            : base(pool)
        {
        }

        /// <summary>
        /// Initializes a new builder which uses <see cref="MemoryPool{T}.Shared"/>
        /// as a default allocator of buffers.
        /// </summary>
        public SequenceBuilder()
            : base(MemoryPool<T>.Shared)
        {
        }

        /// <inheritdoc />
        ReadOnlySequence<T> IReadOnlySequenceSource<T>.Sequence => BufferHelpers.ToReadOnlySequence(this);
    }
}