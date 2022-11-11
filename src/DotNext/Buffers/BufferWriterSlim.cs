using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents stack-allocated buffer writer.
/// </summary>
/// <remarks>
/// This type is similar to <see cref="PooledArrayBufferWriter{T}"/> and <see cref="PooledBufferWriter{T}"/>
/// classes but it tries to avoid on-heap allocation. Moreover, it can use pre-allocated stack
/// memory as a initial buffer used for writing. If builder requires more space then pooled
/// memory used.
/// </remarks>
/// <typeparam name="T">The type of the elements in the memory.</typeparam>
/// <seealso cref="PooledArrayBufferWriter{T}"/>
/// <seealso cref="PooledBufferWriter{T}"/>
/// <seealso cref="SparseBufferWriter{T}"/>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"WrittenCount = {{{nameof(WrittenCount)}}}, FreeCapacity = {{{nameof(FreeCapacity)}}}, Overflow = {{{nameof(Overflow)}}}")]
public ref partial struct BufferWriterSlim<T>
{
    private readonly Span<T> initialBuffer;
    private readonly MemoryAllocator<T>? allocator;
    private MemoryOwner<T> extraBuffer;
    private int position;

    /// <summary>
    /// Initializes growable buffer.
    /// </summary>
    /// <param name="buffer">Pre-allocated buffer used by this builder.</param>
    /// <param name="allocator">The memory allocator used to rent the memory blocks.</param>
    /// <remarks>
    /// The builder created with this constructor is growable. However, additional memory will not be
    /// requested using <paramref name="allocator"/> while <paramref name="buffer"/> space is sufficient.
    /// If <paramref name="allocator"/> is <see langword="null"/> then <see cref="ArrayPool{T}.Shared"/>
    /// is used for memory pooling.
    /// </remarks>
    public BufferWriterSlim(Span<T> buffer, MemoryAllocator<T>? allocator = null)
    {
        initialBuffer = buffer;
        this.allocator = allocator;
        extraBuffer = default;
        position = 0;
    }

    /// <summary>
    /// Initializes growable buffer.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the internal buffer.</param>
    /// <param name="allocator">The memory allocator used to rent the memory blocks.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than zero.</exception>
    public BufferWriterSlim(int initialCapacity, MemoryAllocator<T>? allocator = null)
    {
        if (initialCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        initialBuffer = default;
        this.allocator = allocator;
        extraBuffer = initialCapacity == 0 ? default : allocator.Invoke(initialCapacity, false);
        position = 0;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private readonly int Overflow => Math.Max(0, position - initialBuffer.Length);

    /// <summary>
    /// Gets the amount of data written to the underlying memory so far.
    /// </summary>
    public readonly int WrittenCount => position;

    /// <summary>
    /// Gets the total amount of space within the underlying memory.
    /// </summary>
    public readonly int Capacity => extraBuffer.IsEmpty ? initialBuffer.Length : extraBuffer.Length;

    /// <summary>
    /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
    /// </summary>
    public readonly int FreeCapacity => Capacity - WrittenCount;

    private readonly bool NoOverflow => position <= initialBuffer.Length;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Span<T> Buffer => NoOverflow ? initialBuffer : extraBuffer.Span;

    /// <summary>
    /// Gets span over constructed memory block.
    /// </summary>
    /// <value>The constructed memory block.</value>
    public readonly ReadOnlySpan<T> WrittenSpan => Buffer.Slice(0, position);

    /// <summary>
    /// Returns the memory to write to that is at least the requested size.
    /// </summary>
    /// <param name="sizeHint">The minimum length of the returned memory.</param>
    /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
    /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        Span<T> result;
        int newSize;
        if (extraBuffer.IsEmpty)
        {
            // need to copy initial buffer
            if (IGrowableBuffer<T>.GetBufferSize(sizeHint, initialBuffer.Length, position, out newSize))
            {
                extraBuffer = allocator.Invoke(newSize, exactSize: false);
                initialBuffer.CopyTo(result = extraBuffer.Span);
            }
            else
            {
                result = initialBuffer;
            }
        }
        else
        {
            // no need to copy initial buffer
            if (IGrowableBuffer<T>.GetBufferSize(sizeHint, extraBuffer.Length, position, out newSize))
                extraBuffer.Resize(newSize, exactSize: false, allocator);

            result = extraBuffer.Span;
        }

        return result.Slice(position);
    }

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
            ThrowArgumentOutOfRangeException();

        if (position > Capacity - count)
            ThrowInvalidOperationException();

        position += count;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException(nameof(count));

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidOperationException() => throw new InvalidOperationException();
    }

    /// <summary>
    /// Writes elements to this buffer.
    /// </summary>
    /// <param name="input">The span of elements to be written.</param>
    /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="input"/> elements to it and this builder is not growable.</exception>
    /// <exception cref="OverflowException">The size of the internal buffer becomes greater than <see cref="int.MaxValue"/>.</exception>
    public void Write(scoped ReadOnlySpan<T> input)
    {
        if (!input.IsEmpty)
        {
            input.CopyTo(GetSpan(input.Length));
            position += input.Length;
        }
    }

    /// <summary>
    /// Adds single element to this builder.
    /// </summary>
    /// <param name="item">The item to be added.</param>
    /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="item"/> to it and this builder is not growable.</exception>
    public void Add(T item)
    {
        MemoryMarshal.GetReference(GetSpan(1)) = item;
        position += 1;
    }

    /// <summary>
    /// Gets the last added item.
    /// </summary>
    /// <param name="item">The last added item.</param>
    /// <returns><see langword="true"/> if this buffer is not empty; otherwise, <see langword="false"/>.</returns>
    public readonly bool TryPeek([MaybeNullWhen(false)] out T? item)
        => WrittenSpan.LastOrNone().TryGet(out item);

    /// <summary>
    /// Attempts to remove the last added item.
    /// </summary>
    /// <param name="item">The removed item.</param>
    /// <returns><see langword="true"/> if the item is removed successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryPop([MaybeNullWhen(false)] out T? item)
    {
        if (position > 0)
        {
            item = Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), --position);
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    /// Attempts to remove a sequence of last added items.
    /// </summary>
    /// <param name="output">The buffer to receive last added items.</param>
    /// <returns><see langword="true"/> if items are removed successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryPop(scoped Span<T> output)
    {
        if (position >= output.Length && Buffer.Slice(position - output.Length, output.Length).TryCopyTo(output))
        {
            position -= output.Length;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the element at the specified zero-based index within this builder.
    /// </summary>
    /// <param name="index">Zero-based index of the element.</param>
    /// <value>The element at the specified index.</value>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero or greater than or equal to <see cref="WrittenCount"/>.</exception>
    public readonly ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)position)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), index);
        }
    }

    /// <summary>
    /// Detaches the underlying buffer with written content from this writer.
    /// </summary>
    /// <param name="owner">The buffer owner.</param>
    /// <returns>
    /// <see langword="true"/> if the written content is in rented buffer because initial buffer overflows;
    /// <see langword="false"/> if the written content is in preallocated buffer.
    /// </returns>
    public bool TryDetachBuffer(out MemoryOwner<T> owner)
    {
        if (NoOverflow)
        {
            owner = default;
            return false;
        }

        owner = extraBuffer;
        owner.Truncate(position);
        position = 0;
        extraBuffer = default;
        return true;
    }

    /// <summary>
    /// lears the data written to the underlying buffer.
    /// </summary>
    /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
    public void Clear(bool reuseBuffer = false)
    {
        initialBuffer.Clear();
        if (!reuseBuffer)
        {
            extraBuffer.Dispose();
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            extraBuffer.Span.Clear();
        }

        position = 0;
    }

    /// <summary>
    /// Releases internal buffer used by this builder.
    /// </summary>
    public void Dispose()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            initialBuffer.Clear();

        extraBuffer.Dispose();
        this = default;
    }

    /// <summary>
    /// Converts this buffer to the string.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="T"/> is <see cref="char"/> then
    /// this method returns constructed string instance.
    /// </remarks>
    /// <returns>The textual representation of this object.</returns>
    public readonly override string ToString() => WrittenSpan.ToString();
}