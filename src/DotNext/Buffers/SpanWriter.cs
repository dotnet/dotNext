using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents simple memory writer backed by <see cref="Span{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the span.</typeparam>
[StructLayout(LayoutKind.Auto)]
public ref struct SpanWriter<T>
{
    private readonly ref T reference;
    private readonly int length;
    private int position;

    /// <summary>
    /// Initializes a new memory writer.
    /// </summary>
    /// <param name="span">The span used to write elements.</param>
    public SpanWriter(Span<T> span)
    {
        reference = ref MemoryMarshal.GetReference(span);
        length = span.Length;
    }

    /// <summary>
    /// Initializes a new memory writer.
    /// </summary>
    /// <param name="reference">Managed pointer to the memory block.</param>
    /// <param name="length">The length of the elements referenced by the pointer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public SpanWriter(ref T reference, int length)
    {
        switch (length)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(length));
            case > 0 when Unsafe.IsNullRef(ref reference):
                throw new ArgumentNullException(nameof(reference));
        }

        this.reference = ref reference;
        this.length = length;
    }

    /// <summary>
    /// Gets the element at the current position in the
    /// underlying memory block.
    /// </summary>
    /// <exception cref="InvalidOperationException">The position of this writer is out of range.</exception>
    public readonly ref T Current
    {
        get
        {
            if ((uint)position >= (uint)length)
                ThrowInvalidOperationException();

            return ref Unsafe.Add(ref reference, position);

            [DoesNotReturn]
            [StackTraceHidden]
            static void ThrowInvalidOperationException() => throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Gets the available space in the underlying span.
    /// </summary>
    public readonly int FreeCapacity => length - position;

    /// <summary>
    /// Gets the number of occupied elements in the underlying span.
    /// </summary>
    public int WrittenCount
    {
        readonly get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)length, nameof(value));

            position = value;
        }
    }

    /// <summary>
    /// Gets the remaining part of the span.
    /// </summary>
    public readonly Span<T> RemainingSpan => MemoryMarshal.CreateSpan(ref Unsafe.Add(ref reference, position), FreeCapacity);

    /// <summary>
    /// Advances the position of this writer.
    /// </summary>
    /// <param name="count">The number of written elements.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the available space in the rest of the memory block.</exception>
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)FreeCapacity, nameof(count));

        position += count;
    }

    /// <summary>
    /// Moves the writer back the specified number of items.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero or greater than <see cref="WrittenCount"/>.</exception>
    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)FreeCapacity, nameof(count));

        position -= count;
    }

    /// <summary>
    /// Sets writer position to the first element.
    /// </summary>
    public void Reset() => position = 0;

    /// <summary>
    /// Gets the span over written elements.
    /// </summary>
    /// <value>The segment of underlying span containing written elements.</value>
    public readonly Span<T> WrittenSpan => MemoryMarshal.CreateSpan(ref reference, position);

    /// <summary>
    /// Gets underlying span.
    /// </summary>
    public readonly Span<T> Span => MemoryMarshal.CreateSpan(ref reference, length);

    /// <summary>
    /// Copies the elements to the underlying span.
    /// </summary>
    /// <param name="input">The span to copy from.</param>
    /// <returns>
    /// <see langword="true"/> if all elements are copied successfully;
    /// <see langword="false"/> if remaining space in the underlying span is not enough to place all elements from <paramref name="input"/>.
    /// </returns>
    public bool TryWrite(scoped ReadOnlySpan<T> input)
    {
        if (!input.TryCopyTo(RemainingSpan))
            return false;

        position += input.Length;
        return true;
    }

    /// <summary>
    /// Copies the elements to the underlying span.
    /// </summary>
    /// <param name="input">The span of elements to copy from.</param>
    /// <returns>The number of written elements.</returns>
    public int Write(scoped ReadOnlySpan<T> input)
    {
        input.CopyTo(RemainingSpan, out var writtenCount);
        position += writtenCount;
        return writtenCount;
    }

    /// <summary>
    /// Puts single element into the underlying span.
    /// </summary>
    /// <param name="item">The item to place.</param>
    /// <returns>
    /// <see langword="true"/> if item has beem placed successfully;
    /// <see langword="false"/> if remaining space in the underlying span is not enough to place the item.
    /// </returns>
    public bool TryAdd(T item)
    {
        if ((uint)position < (uint)length)
        {
            Unsafe.Add(ref reference, position++) = item;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Puts single element into the underlying span.
    /// </summary>
    /// <param name="item">The item to place.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the item.</exception>
    public void Add(T item) => Add() = item;

    /// <summary>
    /// Adds single element and returns a reference to it.
    /// </summary>
    /// <returns>The reference to the added element.</returns>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the item.</exception>
    public ref T Add()
    {
        if ((uint)position >= (uint)length)
            ThrowInternalBufferOverflowException();

        return ref Unsafe.Add(ref reference, position++);

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInternalBufferOverflowException() => throw new InternalBufferOverflowException(ExceptionMessages.NotEnoughMemory);
    }

    /// <summary>
    /// Obtains the portion of underlying span and marks it as written.
    /// </summary>
    /// <param name="count">The size of the segment.</param>
    /// <param name="segment">The portion of the underlying span.</param>
    /// <returns>
    /// <see langword="true"/> if segment is obtained successfully;
    /// <see langword="false"/> if remaining space in the underlying span is not enough to place <paramref name="count"/> elements.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public bool TrySlide(int count, out Span<T> segment)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newLength = position + count;
        if ((uint)newLength <= (uint)length)
        {
            segment = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref reference, position), count);
            position = newLength;
            return true;
        }

        segment = default;
        return false;
    }

    /// <summary>
    /// Obtains the portion of underlying span and marks it as written.
    /// </summary>
    /// <param name="count">The size of the segment.</param>
    /// <returns>The portion of the underlying span.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <see cref="FreeCapacity"/>.</exception>
    public Span<T> Slide(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newLength = position + count;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)newLength, (uint)length, nameof(count));

        var result = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref reference, position), count);
        position = newLength;
        return result;
    }

    /// <summary>
    /// Writes a portion of data.
    /// </summary>
    /// <param name="action">The action responsible for writing elements.</param>
    /// <param name="arg">The state to be passed to the action.</param>
    /// <param name="count">The number of the elements to be written.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <see cref="FreeCapacity"/>.</exception>
    [CLSCompliant(false)]
    public unsafe void Write<TArg>(delegate*<TArg, Span<T>, void> action, TArg arg, int count)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newLength = position + count;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)newLength, (uint)length, nameof(count));

        var buffer = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref reference, position), count);
        action(arg, buffer);
        position = newLength;
    }

    /// <summary>
    /// Attempts to write a portion of data.
    /// </summary>
    /// <param name="action">The action responsible for writing elements.</param>
    /// <param name="arg">The state to be passed to the action.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <returns><see langword="true"/> if all elements are written successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
    [CLSCompliant(false)]
    public unsafe bool TryWrite<TArg>(delegate*<TArg, Span<T>, out int, bool> action, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!action(arg, RemainingSpan, out var writtenCount))
            return false;

        position += writtenCount;
        return true;
    }

    /// <summary>
    /// Gets the textual representation of the written content.
    /// </summary>
    /// <returns>The textual representation of the written content.</returns>
    public readonly override string ToString() => WrittenSpan.ToString();
}