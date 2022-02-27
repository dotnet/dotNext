using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using static Runtime.Intrinsics;

/// <summary>
/// Represents memory writer that is backed by the array obtained from the pool.
/// </summary>
/// <remarks>
/// This class provides additional methods for access to array segments in contrast to <see cref="PooledBufferWriter{T}"/>.
/// </remarks>
/// <typeparam name="T">The data type that can be written.</typeparam>
public sealed class PooledArrayBufferWriter<T> : BufferWriter<T>, ISupplier<ArraySegment<T>>, IList<T>
{
    private readonly ArrayPool<T> pool;
    private T[] buffer;

    /// <summary>
    /// Initializes a new writer with the specified initial capacity.
    /// </summary>
    /// <param name="pool">The array pool.</param>
    /// <param name="initialCapacity">The initial capacity of the writer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
    [Obsolete("Use init-only properties to set the capacity and allocator")]
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
    [Obsolete("Use init-only properties to set the capacity and pool")]
    public PooledArrayBufferWriter(ArrayPool<T> pool)
    {
        this.pool = pool;
        buffer = Array.Empty<T>();
    }

    /// <summary>
    /// Initializes a new writer with the specified initial capacity and <see cref="ArrayPool{T}.Shared"/>
    /// as the array pooling mechanism.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the writer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than or equal to zero.</exception>
    [Obsolete("Use init-only properties to set the capacity and pool")]
    public PooledArrayBufferWriter(int initialCapacity)
        : this(ArrayPool<T>.Shared, initialCapacity)
    {
    }

    /// <summary>
    /// Initializes a new writer with the default initial capacity and <see cref="ArrayPool{T}.Shared"/>
    /// as the array pooling mechanism.
    /// </summary>
    /// <seealso cref="BufferPool"/>
    /// <seealso cref="Capacity"/>
    public PooledArrayBufferWriter()
    {
        pool = ArrayPool<T>.Shared;
        buffer = Array.Empty<T>();
    }

    /// <summary>
    /// Sets the array pool that will be used to rent the internal buffer.
    /// </summary>
    /// <remarks>
    /// It is recommended to initialize this property before <see cref="Capacity"/>.
    /// <see langword="null"/> value is the same as <see cref="ArrayPool{T}.Shared"/>.
    /// </remarks>
    public ArrayPool<T>? BufferPool
    {
        init
        {
            value ??= ArrayPool<T>.Shared;

            var length = buffer.Length;

            // cover situation when Capacity initializer called before this initializer
            if (length > 0)
            {
                pool.Return(buffer); // no need to clear fresh array
                buffer = value.Rent(length);
            }

            pool = value;
        }
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
    public new ref T this[int index] => ref this[(long)index];

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element to retrieve.</param>
    /// <value>The element at the specified index.</value>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public ref T this[long index]
    {
        get
        {
            ThrowIfDisposed();
            if ((ulong)index >= (ulong)position)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), (nint)index);
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
        if ((uint)index >= (uint)position)
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
        => Insert(index, MemoryMarshal.CreateReadOnlySpan(ref item, 1));

    /// <summary>
    /// Inserts the elements into this buffer at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <param name="items">The span whose elements should be inserted into this buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public void Insert(int index, ReadOnlySpan<T> items)
    {
        ThrowIfDisposed();
        if ((uint)index > (uint)position)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (items.IsEmpty)
            goto exit;

        if (GetLength(buffer) == 0)
        {
            buffer = pool.Rent(items.Length);
        }
        else if (position + items.Length <= GetLength(buffer))
        {
            Array.Copy(buffer, index, buffer, index + items.Length, position - index);
        }
        else
        {
            var newBuffer = pool.Rent(buffer.Length + items.Length);
            Array.Copy(buffer, 0, newBuffer, 0, index);
            Array.Copy(buffer, index, newBuffer, index + items.Length, buffer.LongLength - index);
            ReleaseBuffer();
            buffer = newBuffer;
        }

        items.CopyTo(buffer.AsSpan(index));
        position += items.Length;

    exit:
        return;
    }

    /// <summary>
    /// Overwrites the elements in this buffer.
    /// </summary>
    /// <param name="index">The zero-based index at which the new elements should be rewritten.</param>
    /// <param name="items">The span whose elements should be added into this buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public void Overwrite(int index, ReadOnlySpan<T> items)
    {
        ThrowIfDisposed();
        if ((uint)index > (uint)position)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (GetLength(buffer) is 0)
        {
            buffer = pool.Rent(items.Length);
        }
        else if (index + items.Length <= GetLength(buffer))
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(buffer, index, position - index);
        }
        else
        {
            var newBuffer = pool.Rent(index + items.Length);
            Array.Copy(buffer, 0, newBuffer, 0, index);
            ReleaseBuffer();
            buffer = newBuffer;
        }

        items.CopyTo(buffer.AsSpan(index));
        position = index + items.Length;
    }

    /// <inheritdoc/>
    T IList<T>.this[int index]
    {
        get => this[index];
        set => this[index] = value;
    }

    /// <inheritdoc/>
    void ICollection<T>.Clear() => Clear(false);

    /// <inheritdoc />
    public override int Capacity
    {
        get
        {
            ThrowIfDisposed();
            return buffer.Length;
        }

        init
        {
            switch (value)
            {
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(value));
                case > 0:
                    buffer = pool.Rent(value);
                    break;
            }
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
    /// Gets the data written to the underlying array so far.
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
    ArraySegment<T> ISupplier<ArraySegment<T>>.Invoke() => WrittenArray;

    private void ReleaseBuffer()
    {
        if (GetLength(buffer) > 0)
            pool.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    /// <summary>
    /// Clears the data written to the underlying memory.
    /// </summary>
    /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public override void Clear(bool reuseBuffer = false)
    {
        ThrowIfDisposed();

        if (!reuseBuffer)
        {
            ReleaseBuffer();
            buffer = Array.Empty<T>();
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(buffer, 0, position);
        }

        position = 0;
    }

    /// <inheritdoc />
    public override MemoryOwner<T> DetachBuffer()
    {
        ThrowIfDisposed();
        MemoryOwner<T> result;
        if (position > 0)
        {
            result = new MemoryOwner<T>(pool, buffer, position);
            buffer = Array.Empty<T>();
            position = 0;
        }
        else
        {
            result = default;
        }

        return result;
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
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

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
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

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
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        CheckAndResizeBuffer(sizeHint);
        return new ArraySegment<T>(buffer, position, buffer.Length - position);
    }

    /// <inheritdoc/>
    public override void AddAll(ICollection<T> items)
    {
        ThrowIfDisposed();

        var count = items.Count;
        if (count <= 0)
            return;

        CheckAndResizeBuffer(count);
        items.CopyTo(buffer, position);
        position += count;
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