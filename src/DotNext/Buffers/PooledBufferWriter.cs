using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents memory writer that uses pooled memory.
    /// </summary>
    /// <typeparam name="T">The data type that can be written.</typeparam>
    public sealed class PooledBufferWriter<T> : BufferWriter<T>, IMemoryOwner<T>
    {
        private readonly MemoryAllocator<T>? allocator;
        private MemoryOwner<T> buffer;

        /// <summary>
        /// Initializes a new writer with the specified initial capacity.
        /// </summary>
        /// <param name="allocator">The allocator of internal buffer.</param>
        /// <param name="initialCapacity">The initial capacity of the writer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public PooledBufferWriter(MemoryAllocator<T>? allocator, int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            this.allocator = allocator;
            buffer = allocator.Invoke(initialCapacity, false);
        }

        /// <summary>
        /// Initializes a new writer with the default initial capacity.
        /// </summary>
        /// <param name="allocator">The allocator of internal buffer.</param>
        public PooledBufferWriter(MemoryAllocator<T>? allocator = null)
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
                return buffer.Memory.Length;
            }
        }

        private Memory<T> GetWrittenMemory()
        {
            ThrowIfDisposed();
            return buffer.Memory.Slice(0, position);
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override ReadOnlyMemory<T> WrittenMemory => GetWrittenMemory();

        /// <inheritdoc />
        Memory<T> IMemoryOwner<T>.Memory => GetWrittenMemory();

        /// <summary>
        /// Clears the data written to the underlying memory.
        /// </summary>
        /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override void Clear(bool reuseBuffer)
        {
            ThrowIfDisposed();

            if (!reuseBuffer)
            {
                buffer.Dispose();
                buffer = default;
            }
            else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                buffer.Memory.Span.Clear();
            }

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

        /// <inheritdoc />
        public override MemoryOwner<T> DetachBuffer()
        {
            ThrowIfDisposed();
            MemoryOwner<T> result;
            if (position > 0)
            {
                result = buffer.Truncate(position);
                buffer = default;
                position = 0;
            }
            else
            {
                result = default;
            }

            return result;
        }

        /// <inheritdoc/>
        private protected override void Resize(int newSize)
        {
            var newBuffer = allocator.Invoke(newSize, false);
            buffer.Memory.CopyTo(newBuffer.Memory);
            buffer.Dispose();
            buffer = newBuffer;
            AllocationCounter?.WriteMetric(newBuffer.Length);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BufferSizeCallback?.Invoke(buffer.Length);
                buffer.Dispose();
                buffer = default;
            }

            base.Dispose(disposing);
        }
    }
}