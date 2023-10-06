using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents simple memory reader backed by <see cref="ReadOnlySpan{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the span.</typeparam>
[StructLayout(LayoutKind.Auto)]
public ref struct SpanReader<T>
{
    private readonly ReadOnlySpan<T> span;
    private int position;

    /// <summary>
    /// Initializes a new memory reader.
    /// </summary>
    /// <param name="span">The span to read from.</param>
    public SpanReader(ReadOnlySpan<T> span)
    {
        this.span = span;
        position = 0;
    }

    /// <summary>
    /// Initializes a new memory reader.
    /// </summary>
    /// <param name="reference">Managed pointer to the memory block.</param>
    /// <param name="length">The length of the elements referenced by the pointer.</param>
    public SpanReader(ref T reference, int length)
    {
        if (Unsafe.IsNullRef(ref reference))
            throw new ArgumentNullException(nameof(reference));

        span = MemoryMarshal.CreateReadOnlySpan(ref reference, length);
        position = 0;
    }

    /// <summary>
    /// Gets the element at the current position in the
    /// underlying memory block.
    /// </summary>
    /// <exception cref="InvalidOperationException">The position of this reader is out of range.</exception>
    public readonly ref readonly T Current
    {
        get
        {
            if ((uint)position >= (uint)span.Length)
                ThrowInvalidOperationException();

            return ref Unsafe.Add(ref MemoryMarshal.GetReference(span), position);

            [DoesNotReturn]
            [StackTraceHidden]
            static void ThrowInvalidOperationException() => throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Gets the number of consumed elements.
    /// </summary>
    public int ConsumedCount
    {
        readonly get => position;
        set
        {
            if ((uint)value > (uint)span.Length)
                ThrowArgumentOutOfRangeException();

            position = value;

            [StackTraceHidden]
            [DoesNotReturn]
            static void ThrowArgumentOutOfRangeException()
                => throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    /// <summary>
    /// Gets the number of unread elements.
    /// </summary>
    public readonly int RemainingCount => span.Length - position;

    /// <summary>
    /// Gets underlying span.
    /// </summary>
    public readonly ReadOnlySpan<T> Span => span;

    /// <summary>
    /// Gets the span over consumed elements.
    /// </summary>
    public readonly ReadOnlySpan<T> ConsumedSpan => span.Slice(0, position);

    /// <summary>
    /// Gets the remaining part of the span.
    /// </summary>
    public readonly ReadOnlySpan<T> RemainingSpan => span.Slice(position);

    /// <summary>
    /// Advances the position of this reader.
    /// </summary>
    /// <param name="count">The number of consumed elements.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the available space in the rest of the memory block.</exception>
    public void Advance(int count)
    {
        if ((uint)count > (uint)RemainingCount)
            ThrowCountOutOfRangeException();

        position += count;
    }

    /// <summary>
    /// Moves the reader back the specified number of items.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero or greater than <see cref="ConsumedCount"/>.</exception>
    public void Rewind(int count)
    {
        if ((uint)count > (uint)position)
            ThrowCountOutOfRangeException();

        position -= count;
    }

    /// <summary>
    /// Sets reader position to the first element.
    /// </summary>
    public void Reset() => position = 0;

    /// <summary>
    /// Copies elements from the underlying span.
    /// </summary>
    /// <param name="output">The span used to write elements from the underlying span.</param>
    /// <returns><see langword="true"/> if size of <paramref name="output"/> is less than or equal to <see cref="RemainingCount"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(scoped Span<T> output)
        => TryRead(output.Length, out var input) && input.TryCopyTo(output);

    /// <summary>
    /// Reads the portion of data from the underlying span.
    /// </summary>
    /// <param name="count">The number of elements to read from the underlying span.</param>
    /// <param name="result">The segment of the underlying span.</param>
    /// <returns><see langword="true"/> if <paramref name="count"/> is less than or equal to <see cref="RemainingCount"/>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public bool TryRead(int count, out ReadOnlySpan<T> result)
    {
        if (count < 0)
            ThrowCountOutOfRangeException();

        var newLength = position + count;

        if ((uint)newLength <= (uint)span.Length)
        {
            result = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(span), position),
                count);
            position = newLength;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Reads single element from the underlying span.
    /// </summary>
    /// <param name="result">The obtained element.</param>
    /// <returns><see langword="true"/> if element is obtained successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryRead([MaybeNullWhen(false)] out T result)
    {
        if ((uint)position < (uint)span.Length)
        {
            result = Unsafe.Add(ref MemoryMarshal.GetReference(span), position++);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Copies elements from the underlying span.
    /// </summary>
    /// <param name="output">The span used to write elements from the underlying span.</param>
    /// <returns>The number of obtained elements.</returns>
    public int Read(scoped Span<T> output)
    {
        RemainingSpan.CopyTo(output, out var writtenCount);
        position += writtenCount;
        return writtenCount;
    }

    /// <summary>
    /// Reads single element from the underlying span.
    /// </summary>
    /// <returns>The element obtained from the span.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    public T Read()
    {
        if (!TryRead(out var result))
            ThrowInternalBufferOverflowException();

        return result;
    }

    /// <summary>
    /// Reads the portion of data from the underlying span.
    /// </summary>
    /// <param name="count">The number of elements to read from the underlying span.</param>
    /// <returns>The portion of data within the underlying span.</returns>
    /// <exception cref="InternalBufferOverflowException"><paramref name="count"/> is greater than <see cref="RemainingCount"/>.</exception>
    public ReadOnlySpan<T> Read(int count)
    {
        if (!TryRead(count, out var result))
            ThrowInternalBufferOverflowException();

        return result;
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowInternalBufferOverflowException() => throw new InternalBufferOverflowException();

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowCountOutOfRangeException() => throw new ArgumentOutOfRangeException("count");

    // TODO: Replace with ArgumentNullException.ThrowIfNull in .NET 8
    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowArgumentNullException() => throw new ArgumentNullException("reader");

    /// <summary>
    /// Decodes the value from the block of memory.
    /// </summary>
    /// <param name="reader">The decoder.</param>
    /// <param name="count">The numbers of elements to read.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is zero.</exception>
    /// <exception cref="InternalBufferOverflowException"><paramref name="count"/> is greater than <see cref="RemainingCount"/>.</exception>
    [CLSCompliant(false)]
    public unsafe TResult Read<TResult>(delegate*<ReadOnlySpan<T>, TResult> reader, int count)
    {
        if (reader is null)
            ThrowArgumentNullException();

        if (!TryRead(count, out var buffer))
            ThrowInternalBufferOverflowException();

        return reader(buffer);
    }

    /// <summary>
    /// Attempts to decode the value from the block of memory.
    /// </summary>
    /// <param name="reader">The decoder.</param>
    /// <param name="count">The numbers of elements to read.</param>
    /// <param name="result">The decoded value.</param>
    /// <typeparam name="TResult">The type of the value to be decoded.</typeparam>
    /// <returns><see langword="true"/> if the value is decoded successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is zero.</exception>
    [CLSCompliant(false)]
    public unsafe bool TryRead<TResult>(delegate*<ReadOnlySpan<T>, TResult> reader, int count, [MaybeNullWhen(false)] out TResult result)
    {
        if (reader is null)
            ThrowArgumentNullException();

        if (TryRead(count, out var buffer))
        {
            result = reader(buffer);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Reads the rest of the memory block.
    /// </summary>
    /// <returns>The rest of the memory block.</returns>
    public ReadOnlySpan<T> ReadToEnd()
    {
        var result = RemainingSpan;
        position = span.Length;
        return result;
    }

    /// <summary>
    /// Gets the textual representation of the written content.
    /// </summary>
    /// <returns>The textual representation of the written content.</returns>
    public readonly override string ToString() => ConsumedSpan.ToString();
}