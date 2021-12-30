using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

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
    [Obsolete("Use init-only properties to set the capacity and allocator")]
    public PooledBufferWriter(MemoryAllocator<T>? allocator, int initialCapacity)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        this.allocator = allocator;
        buffer = allocator.Invoke(initialCapacity, exactSize: false);
    }

    /// <summary>
    /// Initializes a new writer with the default initial capacity.
    /// </summary>
    /// <param name="allocator">The allocator of internal buffer.</param>
    [Obsolete("Use init-only properties to set the capacity and allocator")]
    public PooledBufferWriter(MemoryAllocator<T>? allocator)
        => this.allocator = allocator;

    /// <summary>
    /// Initializes a new empty writer.
    /// </summary>
    /// <seealso cref="BufferAllocator"/>
    /// <seealso cref="Capacity"/>
    public PooledBufferWriter()
    {
    }

    /// <summary>
    /// Sets the allocator of internal buffer.
    /// </summary>
    /// <remarks>
    /// It is recommended to initialize this property before <see cref="Capacity"/>.
    /// </remarks>
    public MemoryAllocator<T>? BufferAllocator
    {
        init => this.allocator = value;
    }

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
                    buffer = allocator.Invoke(value, exactSize: false);
                    break;
            }
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
    public override void Clear(bool reuseBuffer = false)
    {
        ThrowIfDisposed();

        if (!reuseBuffer)
        {
            buffer.Dispose();
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer.Span.Clear();
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
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

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
            result = buffer;
            buffer = default;
            result.Truncate(position);
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
        buffer.Resize(newSize, false, allocator);
        AllocationCounter?.WriteMetric(buffer.Length);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BufferSizeCallback?.Invoke(buffer.Length);
            buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}