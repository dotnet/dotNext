using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents simple memory reader backed by <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public ref struct SpanReader<T>
    {
        // TODO: Support of BinaryPrimitives should be added using function pointers in C# 9
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
        /// Gets the element at the current position in the
        /// underlying memory block.
        /// </summary>
        /// <exception cref="InvalidOperationException">The position of this reader is out of range.</exception>
        public readonly ref readonly T Current
        {
            get
            {
                if (position >= span.Length)
                    throw new InvalidOperationException();

                return ref Unsafe.Add(ref MemoryMarshal.GetReference(span), position);
            }
        }

        /// <summary>
        /// Gets the number of consumed elements.
        /// </summary>
        public readonly int ConsumedCount => position;

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
        /// Sets reader position to the first element.
        /// </summary>
        public void Reset() => position = 0;

        /// <summary>
        /// Copies elements from the underlying span.
        /// </summary>
        /// <param name="output">The span used to write elements from the underlying span.</param>
        /// <returns><see langword="true"/> if size of <paramref name="output"/> is less than or equal to <see cref="RemainingCount"/>; otherwise, <see langword="false"/>.</returns>
        public bool TryRead(Span<T> output)
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
                throw new ArgumentOutOfRangeException(nameof(count));

            var newLength = checked(position + count);

            if (newLength <= span.Length)
            {
                result = span.Slice(position, count);
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
            var newLength = checked(position + 1);

            if (newLength <= span.Length)
            {
                result = Unsafe.Add(ref MemoryMarshal.GetReference(span), position);
                position = newLength;
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
        public int Read(Span<T> output)
        {
            span.Slice(position).CopyTo(output, out var writtenCount);
            position += writtenCount;
            return writtenCount;
        }

        /// <summary>
        /// Reads single element from the underlying span.
        /// </summary>
        /// <returns>The element obtained from the span.</returns>
        /// <exception cref="EndOfStreamException">The end of memory block is reached.</exception>
        public T Read() => TryRead(out var result) ? result : throw new EndOfStreamException();

        /// <summary>
        /// Reads the portion of data from the underlying span.
        /// </summary>
        /// <param name="count">The number of elements to read from the underlying span.</param>
        /// <returns>The portion of data within the underlying span.</returns>
        /// <exception cref="EndOfStreamException"><paramref name="count"/> is greater than <see cref="RemainingCount"/>.</exception>
        public ReadOnlySpan<T> Read(int count)
            => TryRead(count, out var result) ? result : throw new EndOfStreamException();

        /// <summary>
        /// Reads the rest of the memory block.
        /// </summary>
        /// <returns>The rest of the memory block.</returns>
        public ReadOnlySpan<T> ReadToEnd()
        {
            var result = span.Slice(position);
            position = span.Length;
            return result;
        }
    }

    /// <summary>
    /// Represents extension methods for <see cref="SpanReader{T}"/> type.
    /// </summary>
    public static class SpanReader
    {
        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="result">The value deserialized from bytes.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>
        /// <see langword="true"/> if memory block contains enough amount of unread bytes to decode the value;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public static unsafe bool TryRead<T>(this ref SpanReader<byte> reader, out T result)
            where T : unmanaged
        {
            if (reader.TryRead(sizeof(T), out var block))
                return MemoryMarshal.TryRead(block, out result);

            result = default;
            return false;
        }

        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The value deserialized from bytes.</returns>
        /// <exception cref="EndOfStreamException">The end of memory block is reached.</exception>
        public static unsafe T Read<T>(this ref SpanReader<byte> reader)
            where T : unmanaged
            => MemoryMarshal.Read<T>(reader.Read(sizeof(T)));
    }
}