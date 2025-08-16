using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

public static partial class Memory
{
    /// <summary>
    /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
    /// </summary>
    /// <param name="chunks">The sequence of memory blocks.</param>
    /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
    /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
    public static ReadOnlySequence<T> ToReadOnlySequence<T>(this IEnumerable<ReadOnlyMemory<T>>? chunks)
    {
        Chunk<T>? head = null, tail = null;

        switch (chunks)
        {
            case null:
                break;
            case ReadOnlyMemory<T>[] array:
                CreateChunks(array.AsSpan(), ref head, ref tail);
                break;
            case List<ReadOnlyMemory<T>> list:
                CreateChunks(CollectionsMarshal.AsSpan(list), ref head, ref tail);
                break;
            case LinkedList<ReadOnlyMemory<T>> list:
                FromLinkedList(list, ref head, ref tail);
                break;
            default:
                ToReadOnlySequenceSlow(chunks, ref head, ref tail);
                break;
        }

        return Chunk<T>.CreateSequence(head, tail);

        static void ToReadOnlySequenceSlow(IEnumerable<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            foreach (var segment in chunks)
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
    /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
    /// </summary>
    /// <param name="chunks">The sequence of memory blocks.</param>
    /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
    /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
    public static ReadOnlySequence<T> ToReadOnlySequence<T>(ReadOnlySpan<ReadOnlyMemory<T>> chunks) // TODO: use params
    {
        switch (chunks)
        {
            case []:
                return ReadOnlySequence<T>.Empty;
            case [var chunk]:
                return new(chunk);
            default:
                Chunk<T>? head = null, tail = null;
                CreateChunks(chunks, ref head, ref tail);
                return Chunk<T>.CreateSequence(head, tail);
        }
    }
    
    private static void CreateChunks<T>(ReadOnlySpan<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
    {
        foreach (ref readonly var segment in chunks)
        {
            if (!segment.IsEmpty)
                Chunk<T>.AddChunk(segment, ref head, ref tail);
        }
    }

    /// <summary>
    /// Constructs a sequence of characters from a collection of strings.
    /// </summary>
    /// <param name="strings">A collection of strings.</param>
    /// <returns>A sequence of characters representing concatenated strings.</returns>
    public static ReadOnlySequence<char> ToReadOnlySequence(this IEnumerable<string?>? strings)
    {
        Chunk<char>? head = null, tail = null;

        switch (strings)
        {
            case null:
                break;
            case List<string?> list:
                CreateChunks(CollectionsMarshal.AsSpan(list), ref head, ref tail);
                break;
            case string?[] array:
                CreateChunks(array.AsSpan(), ref head, ref tail);
                break;
            default:
                ToReadOnlySequenceSlow(strings, ref head, ref tail);
                break;
        }

        return Chunk<char>.CreateSequence(head, tail);

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
    /// Constructs a sequence of characters from a collection of strings.
    /// </summary>
    /// <param name="strings">A collection of strings.</param>
    /// <returns>A sequence of characters representing concatenated strings.</returns>
    public static ReadOnlySequence<char> ToReadOnlySequence(ReadOnlySpan<string?> strings) // TODO: Use params
    {
        switch (strings)
        {
            case []:
                return ReadOnlySequence<char>.Empty;
            case [var str]:
                return new(str.AsMemory());
            default:
                Chunk<char>? head = null, tail = null;
                CreateChunks(strings, ref head, ref tail);
                return Chunk<char>.CreateSequence(head, tail);
        }
    }
    
    private static void CreateChunks(ReadOnlySpan<string?> strings, ref Chunk<char>? head, ref Chunk<char>? tail)
    {
        foreach (var str in strings)
        {
            if (str is { Length: > 0 })
                Chunk<char>.AddChunk(str.AsMemory(), ref head, ref tail);
        }
    }

    /// <summary>
    /// Gets a sequence of characters written to the builder.
    /// </summary>
    /// <param name="builder">A string builder.</param>
    /// <returns>A sequence of characters written to the builder.</returns>
    public static ReadOnlySequence<char> ToReadOnlySequence(this StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        Chunk<char>? head = null, tail = null;

        foreach (var chunk in builder.GetChunks())
        {
            if (chunk.IsEmpty is false)
                Chunk<char>.AddChunk(chunk, ref head, ref tail);
        }

        if (head is null || tail is null)
            return ReadOnlySequence<char>.Empty;

        return ReferenceEquals(head, tail)
            ? new(head.Memory)
            : Chunk<char>.CreateSequence(head, tail);
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
    public static void CopyTo<T>(this in ReadOnlySequence<T> source, Span<T> destination, out int writtenCount)
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

    /// <summary>
    /// Copies the contents from the source sequence into a destination span.
    /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <param name="destination">Destination memory.</param>
    /// <param name="consumed">The position within <paramref name="source"/> that represents the end of <paramref name="destination"/>.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The number of copied elements.</returns>
    public static int CopyTo<T>(this in ReadOnlySequence<T> source, Span<T> destination, out SequencePosition consumed)
    {
        var writtenCount = 0;
        ReadOnlyMemory<T> block;

        for (var position = consumed = source.Start;
             source.TryGet(ref position, out block) && block.Length <= destination.Length;
             consumed = position,
             writtenCount += block.Length)
        {
            block.Span.CopyTo(destination);
            destination = destination.Slice(block.Length);
        }

        // copy the last segment
        if (block.Length > destination.Length)
        {
            block = block.Slice(0, destination.Length);
            consumed = source.GetPosition(destination.Length, consumed);
        }
        else
        {
            consumed = source.End;
        }

        block.Span.CopyTo(destination);
        writtenCount += block.Length;
        
        return writtenCount;
    }

    /// <summary>
    /// Tries to get a contiguous block of memory from the specified sequence of elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the block.</typeparam>
    /// <param name="sequence">The sequence of elements to read from.</param>
    /// <param name="count">The size of contiguous block.</param>
    /// <param name="span">The contiguous block of elements.</param>
    /// <returns><see langword="true"/> if contiguous block of elements is obtained successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetBlock<T>(this in ReadOnlySequence<T> sequence, int count, out ReadOnlyMemory<T> span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        span = sequence.First;
        if (span.Length >= count)
        {
            span = span.Slice(0, count);
            return true;
        }

        return false;
    }
}