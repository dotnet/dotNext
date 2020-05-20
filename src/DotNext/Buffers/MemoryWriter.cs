using System;
using System.Buffers;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents memory-backed output sink which <typeparamref name="T"/> data can be written.
    /// </summary>
    /// <typeparam name="T">The data type that can be written.</typeparam>
    public abstract class MemoryWriter<T> : Disposable, IBufferWriter<T>, IConvertible<ReadOnlyMemory<T>>
    {
        /// <summary>
        /// Represents default initial buffer size.
        /// </summary>
        private protected const int DefaultInitialBufferSize = 256;

        /// <summary>
        /// Represents position of write cursor.
        /// </summary>
        private protected int position;

        /// <summary>
        /// Initializes a new memory writer.
        /// </summary>
        private protected MemoryWriter()
        {
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract ReadOnlyMemory<T> WrittenMemory { get; }

        /// <inheritdoc/>
        ReadOnlyMemory<T> IConvertible<ReadOnlyMemory<T>>.Convert() => WrittenMemory;

        /// <summary>
        /// Gets the amount of data written to the underlying memory so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public int WrittenCount
        {
            get
            {
                ThrowIfDisposed();
                return position;
            }
        }

        /// <summary>
        /// Gets the total amount of space within the underlying memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract int Capacity { get; }

        /// <summary>
        /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public int FreeCapacity
        {
            get
            {
                ThrowIfDisposed();
                return Capacity - WrittenCount;
            }
        }

        /// <summary>
        /// Clears the data written to the underlying memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract void Clear();

        /// <summary>
        /// Notifies this writer that <paramref name="count"/> of data items were written.
        /// </summary>
        /// <param name="count">The number of data items written to the underlying buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">Attempts to advance past the end of the underlying buffer.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (position > Capacity - count)
                throw new InvalidOperationException();
            position += count;
        }

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract Memory<T> GetMemory(int sizeHint = 0);

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public virtual Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

        /// <summary>
        /// Reallocates internal buffer.
        /// </summary>
        /// <param name="newSize">A new size of internal buffer.</param>
        private protected abstract void Resize(int newSize);

        /// <summary>
        /// Ensures capacity of internal buffer.
        /// </summary>
        /// <param name="sizeHint">The requested size of the buffer.</param>
        private protected void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0)
                sizeHint = 1;
            if (sizeHint > FreeCapacity)
            {
                int currentLength = Capacity, growBy = Math.Max(currentLength, sizeHint);
                if (currentLength == 0)
                    growBy = Math.Max(growBy, DefaultInitialBufferSize);
                var newSize = currentLength + growBy;
                if ((uint)newSize > int.MaxValue)
                {
                    newSize = currentLength + sizeHint;
                    if ((uint)newSize > int.MaxValue)
                        throw new OutOfMemoryException();
                }

                Resize(newSize);
            }
        }
    }
}