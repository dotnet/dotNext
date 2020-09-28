using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents memory writer that is backed by the array obtained from the pool.
    /// </summary>
    /// <remarks>
    /// This class provides additional methods for access to array segments in contrast to <see cref="PooledBufferWriter{T}"/>.
    /// </remarks>
    /// <typeparam name="T">The data type that can be written.</typeparam>
    public sealed class PooledArrayBufferWriter<T> : MemoryWriter<T>, IConvertible<ArraySegment<T>>, IList<T>
    {
        private readonly ArrayPool<T> pool;
        private T[] buffer;

        /// <summary>
        /// Initializes a new writer with the specified initial capacity.
        /// </summary>
        /// <param name="pool">The array pool.</param>
        /// <param name="initialCapacity">The initial capacity of the writer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public PooledArrayBufferWriter(ArrayPool<T> pool, int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            this.pool = pool;
            buffer = pool.Rent(initialCapacity);
        }

        /// <summary>
        /// Initializes a new writer with the default initial capacity.
        /// </summary>
        /// <param name="pool">The array pool.</param>
        public PooledArrayBufferWriter(ArrayPool<T> pool)
        {
            this.pool = pool;
            buffer = Array.Empty<T>();
        }

        /// <summary>
        /// Initializes a new writer with the default initial capacity and <see cref="ArrayPool{T}.Shared"/>
        /// as the array pooling mechanism.
        /// </summary>
        public PooledArrayBufferWriter()
            : this(ArrayPool<T>.Shared)
        {
        }

        /// <summary>
        /// Initializes a new writer with the specified initial capacity and <see cref="ArrayPool{T}.Shared"/>
        /// as the array pooling mechanism.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the writer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public PooledArrayBufferWriter(int initialCapacity)
            : this(ArrayPool<T>.Shared, initialCapacity)
        {
        }

        /// <inheritdoc/>
        int ICollection<T>.Count => WrittenCount;

        /// <inheritdoc/>
        bool ICollection<T>.IsReadOnly => false;

        /// <inheritdoc/>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
            => WrittenMemory.CopyTo(array.AsMemory(arrayIndex));

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to retrieve.</param>
        /// <value>The element at the specified index.</value>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public new ref T this[int index]
        {
            get
            {
                ThrowIfDisposed();
                if (index < 0 || index >= position)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref buffer[index];
            }
        }

        /// <inheritdoc/>
        int IList<T>.IndexOf(T item)
        {
            ThrowIfDisposed();
            return Array.IndexOf(buffer, item, 0, position);
        }

        /// <inheritdoc/>
        bool ICollection<T>.Contains(T item)
        {
            ThrowIfDisposed();
            return Array.IndexOf(buffer, item, 0, position) >= 0;
        }

        private void RemoveAt(int index)
        {
            Array.Copy(buffer, index + 1L, buffer, index, position - index - 1L);
            buffer[position - 1] = default!;

            if (--position == 0)
            {
                ReleaseBuffer();
                buffer = Array.Empty<T>();
            }
        }

        /// <inheritdoc/>
        void IList<T>.RemoveAt(int index)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= position)
                throw new ArgumentOutOfRangeException(nameof(index));
            RemoveAt(index);
        }

        /// <inheritdoc/>
        bool ICollection<T>.Remove(T item)
        {
            ThrowIfDisposed();
            var index = Array.IndexOf(buffer, item, 0, position);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        /// <inheritdoc/>
        void IList<T>.Insert(int index, T item)
        {
            ThrowIfDisposed();
            if (index < 0 || index > position)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (position == 0)
            {
                if (buffer.LongLength == 0L)
                    buffer = pool.Rent(1);
            }
            else if (position < buffer.LongLength)
            {
                Array.Copy(buffer, index, buffer, index + 1, position - index);
            }
            else
            {
                var newBuffer = pool.Rent(buffer.Length + 1);
                Array.Copy(buffer, 0, newBuffer, 0, Math.Min(index + 1, buffer.LongLength));
                Array.Copy(buffer, index, newBuffer, index + 1, buffer.LongLength - index);
                ReleaseBuffer();
                buffer = newBuffer;
            }

            buffer[index] = item;
            position += 1;
        }

        /// <inheritdoc/>
        T IList<T>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
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
                return buffer.Length;
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
                return new Memory<T>(buffer, 0, position);
            }
        }

        /// <summary>
        /// Gets the daya written to the underlying array so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public ArraySegment<T> WrittenArray
        {
            get
            {
                ThrowIfDisposed();
                return new ArraySegment<T>(buffer, 0, position);
            }
        }

        /// <inheritdoc/>
        ArraySegment<T> IConvertible<ArraySegment<T>>.Convert() => WrittenArray;

        private void ReleaseBuffer()
        {
            if (buffer.Length > 0)
                pool.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        /// <summary>
        /// Clears the data written to the underlying buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override void Clear() => Clear(false);   // TODO: Remove this method in future

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
                ReleaseBuffer();
                buffer = Array.Empty<T>();
            }
            else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                if (buffer.LongLength <= int.MaxValue)
                {
                    Array.Clear(buffer, 0, buffer.Length);
                }
                else
                {
                    Array.Fill(buffer, default!);
                }
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
            return buffer.AsMemory(position);
        }

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public override Span<T> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return buffer.AsSpan(position);
        }

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public ArraySegment<T> GetArray(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return new ArraySegment<T>(buffer, position, buffer.Length - position);
        }

        /// <summary>
        /// Removes the specified number of elements from the tail of this buffer.
        /// </summary>
        /// <param name="count">The number of elements to be removed from the tail of this buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void RemoveLast(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            ThrowIfDisposed();

            if (count >= position)
            {
                ReleaseBuffer();
                buffer = Array.Empty<T>();
                position = 0;
            }
            else if (count > 0)
            {
                var newSize = position - count;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    Array.Clear(buffer, newSize, position - newSize);
                }

                position = newSize;
            }
        }

        /// <summary>
        /// Removes the specified number of elements from the head of this buffer.
        /// </summary>
        /// <param name="count">The number of elements to be removed from the head of this buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void RemoveFirst(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            ThrowIfDisposed();

            if (count >= position)
            {
                ReleaseBuffer();
                buffer = Array.Empty<T>();
                position = 0;
            }
            else if (count > 0)
            {
                var newSize = position - count;
                var newBuffer = pool.Rent(newSize);
                Array.Copy(buffer, count, newBuffer, 0, newSize);
                ReleaseBuffer();
                buffer = newBuffer;
                position = newSize;
            }
        }

        /// <inheritdoc/>
        private protected override void Resize(int newSize)
        {
            var newBuffer = pool.Rent(newSize);
            buffer.CopyTo(newBuffer, 0);
            ReleaseBuffer();
            buffer = newBuffer;
            AllocationCounter?.WriteMetric(newBuffer.LongLength);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BufferSizeCallback?.Invoke(buffer.Length);
                ReleaseBuffer();
                buffer = Array.Empty<T>();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Represents extension methods for <see cref="PooledArrayBufferWriter{T}"/> class.
    /// </summary>
    public static class PooledArrayBufferWriter
    {
        /// <summary>
        /// Gets written content as read-only stream.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The stream representing written bytes.</returns>
        [Obsolete("Use DotNext.IO.StreamSource.AsStream in combination with WrittenArray or WrittenMemory property instead", true)]
        public static Stream GetWrittenBytesAsStream(PooledArrayBufferWriter<byte> writer)
            => IO.StreamSource.GetWrittenBytesAsStream(writer);
    }
}