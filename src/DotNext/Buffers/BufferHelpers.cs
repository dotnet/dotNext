using System;
using System.Buffers;
using System.Collections.Generic;

namespace DotNext.Buffers
{
    public static class BufferHelpers
    {
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

        // TODO: Need writer for StringBuilder but it will be available in .NET Core 5

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
        public static string BuildString(this MemoryWriter<char> writer)
        {
            var span = writer.WrittenMemory.Span;
            return span.IsEmpty ? string.Empty : new string(span);
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

            // optimal path where we can avoid allocation of the delegate instance
            if (sequence.IsSingleSegment)
                return new string(sequence.FirstSpan);

            // TODO: Must be replaced with method pointer in future versions of .NET
            return string.Create(checked((int)sequence.Length), sequence, ReadToEnd);

            static void ReadToEnd(Span<char> output, ReadOnlySequence<char> input)
                => input.CopyTo(output);
        }
    }
}