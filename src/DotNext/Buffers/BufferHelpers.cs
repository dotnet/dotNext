using System;
using System.Buffers;
using System.Collections.Generic;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents helper methods to work with various buffer representations.
    /// </summary>
    public static class BufferHelpers
    {
        private static readonly SpanAction<char, IGrowableBuffer<char>> InitializeStringFromWriter = InitializeString;
        private static readonly SpanAction<char, ReadOnlySequence<char>> InitializeStringFromSequence = InitializeString;

        private static void InitializeString(Span<char> output, IGrowableBuffer<char> input)
            => input.CopyTo(output);

        private static void InitializeString(Span<char> output, ReadOnlySequence<char> input)
            => input.CopyTo(output);

        /// <summary>
        /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="chunks">The sequence of memory blocks.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> ToReadOnlySequence<T>(this IEnumerable<ReadOnlyMemory<T>> chunks)
        {
            Chunk<T>? head = null, tail = null;
            foreach (var segment in chunks)
            {
                if (!segment.IsEmpty)
                    Chunk<T>.AddChunk(segment, ref head, ref tail);
            }

            if (head is null || tail is null)
                return ReadOnlySequence<T>.Empty;

            if (ReferenceEquals(head, tail))
                return new ReadOnlySequence<T>(head.Memory);

            return Chunk<T>.CreateSequence(head, tail);
        }

        /// <summary>
        /// Converts two memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> Concat<T>(this ReadOnlyMemory<T> first, ReadOnlyMemory<T> second)
        {
            if (first.IsEmpty)
                return second.IsEmpty ? ReadOnlySequence<T>.Empty : new ReadOnlySequence<T>(second);

            if (second.IsEmpty)
                return new ReadOnlySequence<T>(first);

            Chunk<T>? head = null, tail = null;
            Chunk<T>.AddChunk(first, ref head, ref tail);
            Chunk<T>.AddChunk(second, ref head, ref tail);
            return Chunk<T>.CreateSequence(head, tail);
        }

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this ArrayBufferWriter<char> writer)
        {
            var span = writer.WrittenSpan;
            return span.IsEmpty ? string.Empty : new string(span);
        }

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this IGrowableBuffer<char> writer)
        {
            var length = writer.WrittenCount;

            if (length == 0L)
                return string.Empty;

            return string.Create(checked((int)length), writer, InitializeStringFromWriter);
        }

        /// <summary>
        /// Constructs the string from non-contiguous buffer.
        /// </summary>
        /// <param name="sequence">The sequence of characters.</param>
        /// <returns>The string constucted from the characters containing in the buffer.</returns>
        public static string BuildString(this in ReadOnlySequence<char> sequence)
        {
            if (sequence.IsEmpty)
                return string.Empty;

            if (sequence.IsSingleSegment)
                return new string(sequence.FirstSpan);

            return string.Create(checked((int)sequence.Length), sequence, InitializeStringFromSequence);
        }

        /// <summary>
        /// Writes single element to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to add.</param>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        public static void Write<T>(this IBufferWriter<T> writer, T value)
        {
            const int count = 1;
            writer.GetSpan(count)[0] = value;
            writer.Advance(count);
        }
    }
}