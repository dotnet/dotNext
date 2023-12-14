using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

public static partial class Memory
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

        switch (chunks)
        {
            case ReadOnlyMemory<T>[] array:
                FromSpan(array.AsSpan(), ref head, ref tail);
                break;
            case List<ReadOnlyMemory<T>> list:
                FromSpan(CollectionsMarshal.AsSpan(list), ref head, ref tail);
                break;
            case LinkedList<ReadOnlyMemory<T>> list:
                FromLinkedList(list, ref head, ref tail);
                break;
            default:
                ToReadOnlySequenceSlow(chunks, ref head, ref tail);
                break;
        }

        if (head is null || tail is null)
            return ReadOnlySequence<T>.Empty;

        if (ReferenceEquals(head, tail))
            return new(head.Memory);

        return Chunk<T>.CreateSequence(head, tail);

        static void ToReadOnlySequenceSlow(IEnumerable<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            foreach (var segment in chunks)
            {
                if (!segment.IsEmpty)
                    Chunk<T>.AddChunk(segment, ref head, ref tail);
            }
        }

        static void FromSpan(ReadOnlySpan<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            foreach (ref readonly var segment in chunks)
            {
                if (!segment.IsEmpty)
                    Chunk<T>.AddChunk(segment, ref head, ref tail);
            }
        }

        static void FromLinkedList(LinkedList<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            for (var current = chunks.First; current is not null; current = current.Next)
            {
                ref readonly var segment = ref current.ValueRef;
                if (!segment.IsEmpty)
                    Chunk<T>.AddChunk(segment, ref head, ref tail);
            }
        }
    }

    /// <summary>
    /// Constructs a sequence of characters from a collection of strings.
    /// </summary>
    /// <param name="strings">A collection of strings.</param>
    /// <returns>A sequence of characters representing concatenated strings.</returns>
    public static ReadOnlySequence<char> ToReadOnlySequence(this IEnumerable<string?> strings)
    {
        Chunk<char>? head = null, tail = null;

        switch (strings)
        {
            case List<string?> list:
                ToReadOnlySequence(CollectionsMarshal.AsSpan(list), ref head, ref tail);
                break;
            case string?[] array:
                ToReadOnlySequence(array.AsSpan(), ref head, ref tail);
                break;
            default:
                ToReadOnlySequenceSlow(strings, ref head, ref tail);
                break;
        }

        if (head is null || tail is null)
            return ReadOnlySequence<char>.Empty;

        if (ReferenceEquals(head, tail))
            return new(head.Memory);

        return Chunk<char>.CreateSequence(head, tail);

        static void ToReadOnlySequence(ReadOnlySpan<string?> strings, ref Chunk<char>? head, ref Chunk<char>? tail)
        {
            foreach (var str in strings)
            {
                if (str is { Length: > 0 })
                    Chunk<char>.AddChunk(str.AsMemory(), ref head, ref tail);
            }
        }

        static void ToReadOnlySequenceSlow(IEnumerable<string?> strings, ref Chunk<char>? head, ref Chunk<char>? tail)
        {
            foreach (var str in strings)
            {
                if (str is { Length: > 0 })
                    Chunk<char>.AddChunk(str.AsMemory(), ref head, ref tail);
            }
        }
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
            return second.IsEmpty ? ReadOnlySequence<T>.Empty : new(second);

        if (second.IsEmpty)
            return new(first);

        Chunk<T>? head = null, tail = null;
        Chunk<T>.AddChunk(first, ref head, ref tail);
        Chunk<T>.AddChunk(second, ref head, ref tail);
        return Chunk<T>.CreateSequence(head, tail);
    }

    /// <summary>
    /// Copies the contents from the source sequence into a destination span.
    /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <param name="destination">Destination memory.</param>
    /// <param name="writtenCount">The number of copied elements.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyTo<T>(this in ReadOnlySequence<T> source, scoped Span<T> destination, out int writtenCount)
    {
        if (source.IsSingleSegment)
        {
            // fast path - single-segment sequence
            source.FirstSpan.CopyTo(destination, out writtenCount);
        }
        else
        {
            // slow path - multisegment sequence
            writtenCount = CopyToSlow(in source, destination);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int CopyToSlow(in ReadOnlySequence<T> source, Span<T> destination)
        {
            int result = 0, subcount;

            for (var position = source.Start; !destination.IsEmpty && source.TryGet(ref position, out var block); result += subcount)
            {
                block.Span.CopyTo(destination, out subcount);
                destination = destination.Slice(subcount);
            }

            return result;
        }
    }
}