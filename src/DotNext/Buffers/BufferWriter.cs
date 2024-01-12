using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Enumerator = Collections.Generic.Enumerator;

/// <summary>
/// Represents memory-backed output sink which <typeparamref name="T"/> data can be written.
/// </summary>
/// <typeparam name="T">The data type that can be written.</typeparam>
[DebuggerDisplay($"WrittenCount = {{{nameof(WrittenCount)}}}, FreeCapacity = {{{nameof(FreeCapacity)}}}")]
public abstract class BufferWriter<T> : Disposable, IBufferWriter<T>, ISupplier<ReadOnlyMemory<T>>, IReadOnlyList<T>, IGrowableBuffer<T>
{
    private const string ElementTypeMeterAttribute = "dotnext.buffers.element";

    private protected readonly TagList measurementTags;

    /// <summary>
    /// Represents position of write cursor.
    /// </summary>
    private protected int position;

    /// <summary>
    /// Initializes a new memory writer.
    /// </summary>
    private protected BufferWriter()
    {
        measurementTags = new() { { ElementTypeMeterAttribute, typeof(T).Name } };
    }

    /// <summary>
    /// Sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public virtual TagList MeasurementTags
    {
        init
        {
            value.Add(ElementTypeMeterAttribute, typeof(T).Name);
            measurementTags = value;
        }
    }

    /// <summary>
    /// Gets the data written to the underlying buffer so far.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public abstract ReadOnlyMemory<T> WrittenMemory { get; }

    /// <inheritdoc/>
    ReadOnlyMemory<T> ISupplier<ReadOnlyMemory<T>>.Invoke() => WrittenMemory;

    /// <summary>
    /// Gets or sets the amount of data written to the underlying memory so far.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than <see cref="Capacity"/>.</exception>
    public int WrittenCount
    {
        get => position;
        set
        {
            if ((uint)value > (uint)Capacity)
                ThrowArgumentOutOfRangeException();

            position = value;

            [DoesNotReturn]
            [StackTraceHidden]
            static void ThrowArgumentOutOfRangeException()
                => throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    /// <inheritdoc />
    long IGrowableBuffer<T>.WrittenCount => WrittenCount;

    /// <inheritdoc />
    void IGrowableBuffer<T>.Write(ReadOnlySpan<T> input)
        => BuffersExtensions.Write(this, input);

    /// <inheritdoc />
    void IGrowableBuffer<T>.CopyTo<TConsumer>(TConsumer consumer)
        => consumer.Invoke(WrittenMemory.Span);

    /// <inheritdoc />
    void IGrowableBuffer<T>.Clear() => Clear();

    /// <inheritdoc />
    int IGrowableBuffer<T>.CopyTo(Span<T> output)
    {
        WrittenMemory.Span.CopyTo(output, out var writtenCount);
        return writtenCount;
    }

    /// <inheritdoc />
    bool IGrowableBuffer<T>.TryGetWrittenContent(out ReadOnlyMemory<T> block)
    {
        block = WrittenMemory;
        return true;
    }

    /// <inheritdoc />
    ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => IsDisposed ? new ValueTask(DisposedTask) : consumer.Invoke(WrittenMemory, token);

    /// <summary>
    /// Writes single element.
    /// </summary>
    /// <param name="item">The element to write.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public void Add(T item)
    {
        MemoryMarshal.GetReference(GetSpan(1)) = item;
        position += 1;
    }

    /// <inheritdoc />
    void IGrowableBuffer<T>.Write(T value) => Add(value);

    /// <summary>
    /// Writes multiple elements.
    /// </summary>
    /// <param name="items">The collection of elements to be copied.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public virtual void AddAll(ICollection<T> items)
    {
        if (items.Count == 0)
            return;

        var span = GetSpan(items.Count);
        int count;
        switch (items)
        {
            case List<T> list:
                CollectionsMarshal.AsSpan(list).CopyTo(span, out count);
                break;
            case T[] array:
                array.AsSpan().CopyTo(span, out count);
                break;
            case ArraySegment<T> segment:
                segment.AsSpan().CopyTo(span, out count);
                break;
            default:
                count = CopyFromCollection(items, span);
                break;
        }

        position += count;

        static int CopyFromCollection(ICollection<T> input, Span<T> output)
        {
            Debug.Assert(output.Length >= input.Count);

            var count = 0;
            using var enumerator = input.GetEnumerator();
            while (count < output.Length && enumerator.MoveNext())
                output[count++] = enumerator.Current;

            return count;
        }
    }

    /// <inheritdoc/>
    int IReadOnlyCollection<T>.Count => WrittenCount;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element to retrieve.</param>
    /// <value>The element at the specified index.</value>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public ref readonly T this[int index] => ref WrittenMemory.Span[index];

    /// <inheritdoc/>
    T IReadOnlyList<T>.this[int index] => this[index];

    /// <summary>
    /// Gets or sets the total amount of space within the underlying memory.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than zero.</exception>
    public abstract int Capacity { get; init; }

    /// <summary>
    /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
    /// </summary>
    public int FreeCapacity => Capacity - WrittenCount;

    /// <summary>
    /// Clears the data written to the underlying memory.
    /// </summary>
    /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public abstract void Clear(bool reuseBuffer = false);

    /// <summary>
    /// Notifies this writer that <paramref name="count"/> of data items were written.
    /// </summary>
    /// <param name="count">The number of data items written to the underlying buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    /// <exception cref="InvalidOperationException">Attempts to advance past the end of the underlying buffer.</exception>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (count < 0)
            ThrowCountOutOfRangeException();

        var newPosition = position + count;
        if ((uint)newPosition > (uint)Capacity)
            ThrowInvalidOperationException();

        position = newPosition;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidOperationException()
            => throw new InvalidOperationException();
    }

    /// <summary>
    /// Moves the writer back the specified number of items.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero or greater than <see cref="WrittenCount"/>.</exception>
    public void Rewind(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if ((uint)count > (uint)position)
            ThrowCountOutOfRangeException();

        position -= count;
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowCountOutOfRangeException()
        => throw new ArgumentOutOfRangeException("count");

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
    /// Transfers ownership of the written memory from this writer to the caller.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for the lifetime of the returned buffer. The current
    /// state of this writer will be reset.
    /// </remarks>
    /// <returns>The object representing all written content.</returns>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public abstract MemoryOwner<T> DetachBuffer();

    /// <summary>
    /// Reallocates internal buffer.
    /// </summary>
    /// <param name="newSize">A new size of internal buffer.</param>
    private protected abstract void Resize(int newSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected void CheckAndResizeBuffer(int sizeHint)
    {
        if (IGrowableBuffer<T>.GetBufferSize(sizeHint, Capacity, position, out sizeHint))
            Resize(sizeHint);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        position = 0;
        base.Dispose(disposing);
    }

    /// <summary>
    /// Gets enumerator over all written elements.
    /// </summary>
    /// <returns>The enumerator over all written elements.</returns>
    public IEnumerator<T> GetEnumerator() => Enumerator.ToEnumerator(WrittenMemory);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the textual representation of this buffer.
    /// </summary>
    /// <returns>The textual representation of this buffer.</returns>
    public override string ToString() => WrittenMemory.ToString();
}