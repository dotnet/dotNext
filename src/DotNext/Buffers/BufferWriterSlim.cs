using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents stack-allocated buffer writer.
/// </summary>
/// <remarks>
/// This type is similar to <see cref="PoolingArrayBufferWriter{T}"/> and <see cref="PoolingBufferWriter{T}"/>
/// classes but it tries to avoid on-heap allocation. Moreover, it can use pre-allocated stack
/// memory as a initial buffer used for writing. If builder requires more space then pooled
/// memory used.
/// </remarks>
/// <typeparam name="T">The type of the elements in the memory.</typeparam>
/// <seealso cref="PoolingArrayBufferWriter{T}"/>
/// <seealso cref="PoolingBufferWriter{T}"/>
/// <seealso cref="SparseBufferWriter{T}"/>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"WrittenCount = {{{nameof(WrittenCount)}}}, FreeCapacity = {{{nameof(FreeCapacity)}}}, Overflow = {{{nameof(Overflow)}}}")]
public ref partial struct BufferWriterSlim<T> : IGrowableBuffer<T>
{
    private readonly Span<T> initialBuffer;
    private readonly MemoryAllocator<T> allocator;
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
        this.allocator = allocator.DefaultIfNull;
    }

    /// <summary>
    /// Initializes growable buffer.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the internal buffer.</param>
    /// <param name="allocator">The memory allocator used to rent the memory blocks.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than zero.</exception>
    public BufferWriterSlim(int initialCapacity, MemoryAllocator<T>? allocator = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        allocator ??= MemoryAllocator<T>.Default;
        this.allocator = allocator;
        extraBuffer = initialCapacity is 0 ? default : allocator.AllocateAtLeast(initialCapacity);
    }

    /// <summary>
    /// Initializes a new buffer writer.
    /// </summary>
    public BufferWriterSlim()
    {
        allocator = MemoryAllocator<T>.Default;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private readonly int Overflow => Math.Max(0, position - initialBuffer.Length);

    /// <summary>
    /// Gets the amount of data written to the underlying memory so far.
    /// </summary>
    public int WrittenCount
    {
        readonly get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)Capacity, nameof(value));

            position = value;
        }
    }

    /// <inheritdoc/>
    readonly long IGrowableBuffer<T>.WrittenCount => WrittenCount;

    /// <summary>
    /// Gets the total amount of space within the underlying memory.
    /// </summary>
    public readonly int Capacity => extraBuffer.IsEmpty ? initialBuffer.Length : extraBuffer.Length;

    /// <summary>
    /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
    /// </summary>
    public readonly int FreeCapacity => Capacity - WrittenCount;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Span<T> Buffer => extraBuffer.IsEmpty ? initialBuffer : extraBuffer.Span;

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
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        return InternalGetSpan(sizeHint);
    }

    /// <inheritdoc/>
    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => GetMemory(sizeHint);
    
    private Memory<T> GetMemory(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        
        if (extraBuffer.Length > 0)
        {
            // no need to copy initial buffer
            EnsureExtraBufferSize(sizeHint);
        }
        else
        {
            // copy everything to the extra buffer, because it's not possible to return the stack pointer
            IGrowableBuffer<T>.GetBufferSize(sizeHint, initialBuffer.Length, position, out var newSize);
            extraBuffer = allocator.AllocateAtLeast(newSize);
            initialBuffer.Slice(0, position).CopyTo(extraBuffer.Span);
        }

        return extraBuffer.Memory.Slice(position);
    } 

    internal Span<T> InternalGetSpan(int sizeHint)
    {
        Debug.Assert(sizeHint >= 0);

        Span<T> result;
        if (extraBuffer.Length > 0)
        {
            // no need to copy initial buffer
            EnsureExtraBufferSize(sizeHint);
            result = extraBuffer.Span;
        }
        else if (IGrowableBuffer<T>.GetBufferSize(sizeHint, initialBuffer.Length, position, out var newSize))
        {
            extraBuffer = allocator.AllocateAtLeast(newSize);
            initialBuffer.CopyTo(result = extraBuffer.Span);
        }
        else
        {
            result = initialBuffer;
        }

        return result.Slice(position);
    }

    private void EnsureExtraBufferSize(int sizeHint)
    {
        if (IGrowableBuffer<T>.GetBufferSize(sizeHint, extraBuffer.Length, position, out sizeHint))
            extraBuffer.Resize(sizeHint, allocator);
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
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newPosition = position + count;
        if (newPosition > Capacity)
            InvalidOperationException.Throw();

        position = newPosition;
    }

    /// <summary>
    /// Moves the writer back the specified number of items.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero or greater than <see cref="WrittenCount"/>.</exception>
    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)position, nameof(count));

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Buffer.Slice(count).Clear();

        position -= count;
    }

    /// <summary>
    /// Writes elements to this buffer.
    /// </summary>
    /// <param name="input">The span of elements to be written.</param>
    /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="input"/> elements to it and this builder is not growable.</exception>
    /// <exception cref="OverflowException">The size of the internal buffer becomes greater than <see cref="int.MaxValue"/>.</exception>
    public void Write(scoped ReadOnlySpan<T> input)
    {
        if (input.Length > 0)
        {
            input.CopyTo(InternalGetSpan(input.Length));
            position += input.Length;
        }
    }

    /// <inheritdoc cref="Write"/>
    public void operator += (scoped ReadOnlySpan<T> input) => Write(input);

    /// <inheritdoc />
    void IConsumer<ReadOnlySpan<T>>.Invoke(ReadOnlySpan<T> input)
        => Write(input);

    /// <summary>
    /// Adds single element to this builder.
    /// </summary>
    /// <param name="item">The item to be added.</param>
    /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="item"/> to it and this builder is not growable.</exception>
    public void Add(T item) => Add() = item;

    /// <inheritdoc/>
    void IGrowableBuffer<T>.Write(T item) => Add() = item;

    /// <summary>
    /// Adds single element and returns a reference to it.
    /// </summary>
    /// <returns>The reference to the added element.</returns>
    /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place extra element.</exception>
    public ref T Add()
    {
        ref T result = ref MemoryMarshal.GetReference(InternalGetSpan(1));
        position += 1;
        return ref result;
    }

    /// <inheritdoc cref="Add(T)"/>
    public void operator += (T item) => Add() = item;

    /// <summary>
    /// Gets the last added item.
    /// </summary>
    /// <param name="item">The last added item.</param>
    /// <returns><see langword="true"/> if this buffer is not empty; otherwise, <see langword="false"/>.</returns>
    public readonly bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (position > 0)
        {
            item = Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), position - 1);
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    /// Attempts to remove the last added item.
    /// </summary>
    /// <param name="item">The removed item.</param>
    /// <returns><see langword="true"/> if the item is removed successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryPop([MaybeNullWhen(false)] out T item)
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

    /// <inheritdoc/>
    readonly int IGrowableBuffer<T>.CopyTo(Span<T> output)
        => WrittenSpan >>> output;

    /// <inheritdoc/>
    readonly void IGrowableBuffer<T>.CopyTo<TConsumer>(TConsumer consumer)
        => consumer.Invoke(WrittenSpan);

    /// <inheritdoc/>
    ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
    {
        // copy everything to the extra buffer
        if (extraBuffer.IsEmpty)
        {
            extraBuffer = allocator.AllocateAtLeast(position);
            initialBuffer.Slice(0, position).CopyTo(extraBuffer.Span);
        }
        
        return consumer.Invoke(extraBuffer.Memory.Slice(0, position), token);
    }

    /// <inheritdoc/>
    ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
        => WriteAsync(input, token);

    private ValueTask WriteAsync(ReadOnlyMemory<T> input, CancellationToken token)
    {
        var task = ValueTask.CompletedTask;
        try
        {
            Write(input.Span);
        }
        catch (Exception e)
        {
            task = ValueTask.FromException(e);
        }

        return task;
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
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)position, nameof(index));

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
        if (extraBuffer.IsEmpty)
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
    /// Detaches or copies the underlying buffer with written content from this writer.
    /// </summary>
    /// <returns>Detached or copied buffer.</returns>
    public MemoryOwner<T> DetachOrCopyBuffer()
    {
        MemoryOwner<T> result;

        if (position is 0)
        {
            result = default;
        }
        else
        {
            if (extraBuffer.IsEmpty)
            {
                result = allocator.AllocateExactly(position);
                initialBuffer.CopyTo(result.Span);
            }
            else
            {
                result = extraBuffer;
                extraBuffer = default;
            }

            result.Truncate(position);
            position = 0;
        }

        return result;
    }

    /// <inheritdoc/>
    bool IGrowableBuffer<T>.TryGetWrittenContent(out ReadOnlyMemory<T> block)
    {
        if (extraBuffer.IsEmpty)
        {
            block = default;
            return false;
        }

        block = extraBuffer.Memory.Slice(0, position);
        return true;
    }

    /// <summary>
    /// Clears the data written to the underlying buffer.
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

    /// <inheritdoc/>
    void IResettable.Reset() => Clear();

    /// <summary>
    /// Writes a collection of elements.
    /// </summary>
    /// <param name="collection">A collection of elements.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/>.</exception>
    public void AddAll(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        ReadOnlySpan<T> input;
        switch (collection)
        {
            case List<T> list:
                input = CollectionsMarshal.AsSpan(list);
                break;
            case T[] array:
                input = array;
                break;
            case string str:
                input = Unsafe.BitCast<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(str.AsMemory()).Span;
                break;
            case ArraySegment<T> segment:
                input = segment;
                break;
            default:
                WriteSlow(collection);
                return;
        }

        Write(input);
    }

    /// <inheritdoc cref="AddAll"/>
    public void operator += (IEnumerable<T> collection) => AddAll(collection);

    private void WriteSlow(IEnumerable<T> collection)
    {
        using var enumerator = collection.GetEnumerator();
        if (collection.TryGetNonEnumeratedCount(out var count))
        {
            var buffer = InternalGetSpan(count);
            for (count = 0; count < buffer.Length && enumerator.MoveNext(); count++)
            {
                buffer[count] = enumerator.Current;
            }

            position += count;
        }

        while (enumerator.MoveNext())
        {
            Add(enumerator.Current);
        }
    }

    /// <summary>
    /// Releases internal buffer used by this builder.
    /// </summary>
    public void Dispose()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            initialBuffer.Clear();
            extraBuffer.Clear(clearBuffer: true);
        }
        else
        {
            extraBuffer.Clear(clearBuffer: false);
        }

        this = default;
    }
    
    /// <inheritdoc cref="IFunctional.DynamicInvoke"/>
    void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
    {
        switch (count)
        {
            case 1:
                Write(args.Immutable<ReadOnlySpan<T>>());
                break;
            case 2:
                result.Mutable<ValueTask>() = WriteAsync(
                    IFunctional.GetArgument<ReadOnlyMemory<T>>(in args, 0),
                    IFunctional.GetArgument<CancellationToken>(in args, 1)
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(count));
        }
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