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
    private readonly ref T reference;
    private readonly int length;
    private int position;

    /// <summary>
    /// Initializes a new memory reader.
    /// </summary>
    /// <param name="span">The span to read from.</param>
    public SpanReader(ReadOnlySpan<T> span)
    {
        reference = ref MemoryMarshal.GetReference(span);
        length = span.Length;
    }

    /// <summary>
    /// Initializes a new memory reader.
    /// </summary>
    /// <param name="reference">Managed pointer to the memory block.</param>
    /// <param name="length">The length of the elements referenced by the pointer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public SpanReader(ref T reference, int length)
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
    /// <exception cref="InvalidOperationException">The position of this reader is out of range.</exception>
    public readonly ref readonly T Current
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
    /// Gets the number of consumed elements.
    /// </summary>
    public int ConsumedCount
    {
        readonly get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)length, nameof(value));

            position = value;
        }
    }

    /// <summary>
    /// Gets the number of unread elements.
    /// </summary>
    public readonly int RemainingCount => length - position;

    /// <summary>
    /// Gets underlying span.
    /// </summary>
    public readonly ReadOnlySpan<T> Span => MemoryMarshal.CreateReadOnlySpan(ref reference, length);

    /// <summary>
    /// Gets the span over consumed elements.
    /// </summary>
    public readonly ReadOnlySpan<T> ConsumedSpan => MemoryMarshal.CreateReadOnlySpan(ref reference, position);

    /// <summary>
    /// Gets the remaining part of the span.
    /// </summary>
    public readonly ReadOnlySpan<T> RemainingSpan
        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref reference, position), RemainingCount);

    /// <summary>
    /// Advances the position of this reader.
    /// </summary>
    /// <param name="count">The number of consumed elements.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the available space in the rest of the memory block.</exception>
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)RemainingCount, nameof(count));

        position += count;
    }

    /// <summary>
    /// Moves the reader back the specified number of items.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero or greater than <see cref="ConsumedCount"/>.</exception>
    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)position, nameof(count));

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
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var newLength = position + count;

        if ((uint)newLength <= (uint)length)
        {
            result = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref reference, position), count);
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
        if ((uint)position < (uint)length)
        {
            result = Unsafe.Add(ref reference, position++);
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
    public ref readonly T Read()
    {
        if ((uint)position >= (uint)length)
            ThrowInternalBufferOverflowException();

        return ref Unsafe.Add(ref reference, position++);
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
        ArgumentNullException.ThrowIfNull(reader);

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
        ArgumentNullException.ThrowIfNull(reader);

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
        position = length;
        return result;
    }

    /// <summary>
    /// Gets the textual representation of the written content.
    /// </summary>
    /// <returns>The textual representation of the written content.</returns>
    public readonly override string ToString() => ConsumedSpan.ToString();
}