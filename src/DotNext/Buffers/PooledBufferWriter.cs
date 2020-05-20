using System;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents memory writer that uses pooled memory.
    /// </summary>
    /// <typeparam name="T">The data type that can be written.</typeparam>
    public sealed class PooledBufferWriter<T> : MemoryWriter<T>
    {
        private readonly MemoryAllocator<T> allocator;
        private MemoryOwner<T> buffer;

        /// <summary>
        /// Initializes a new writer with the specified initial capacity.
        /// </summary>
        /// <param name="allocator">The allocator of internal buffer.</param>
        /// <param name="initialCapacity">The initial capacity of the writer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public PooledBufferWriter(MemoryAllocator<T> allocator, int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            this.allocator = allocator;
            buffer = allocator(initialCapacity);
        }

        /// <summary>
        /// Initializes a new writer with the default initial capacity.
        /// </summary>
        /// <param name="allocator">The allocator of internal buffer.</param>
        public PooledBufferWriter(MemoryAllocator<T> allocator)
        {
            this.allocator = allocator;
        }

        /// <summary>
        /// Gets the total amount of space within the underlying memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override int Capacity
        {
            get
            {
                ThrowIfDisposed();
                return buffer.RawMemory.Length;
            }
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override ReadOnlyMemory<T> WrittenMemory
        {
            get
            {
                ThrowIfDisposed();
                return buffer.RawMemory.Slice(0, position);
            }
        }

        /// <summary>
        /// Clears the data written to the underlying buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override void Clear()
        {
            ThrowIfDisposed();
            buffer.Dispose();
            buffer = default;
            position = 0;
        }

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return buffer.Memory.Slice(position);
        }

        /// <inheritdoc/>
        private protected override void Resize(int newSize)
        {
            var newBuffer = allocator(newSize);
            buffer.Memory.CopyTo(newBuffer.Memory);
            buffer.Dispose();
            buffer = newBuffer;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer.Dispose();
                buffer = default;
            }

            base.Dispose(disposing);
        }
    }
}