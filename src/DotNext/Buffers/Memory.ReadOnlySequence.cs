using System.Buffers;
using System.Collections;
using System.Diagnostics;
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
    public static ReadOnlySequence<T> Concat<T>(this IEnumerable<ReadOnlyMemory<T>>? chunks)
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

        static void FromLinkedList(LinkedList<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            for (var current = chunks.First; current is not null; current = current.Next)
            {
                ref readonly var segment = ref current.ValueRef;
                if (segment.Length > 0)
                    Chunk<T>.AddChunk(segment, ref head, ref tail);
            }
        }

        static void ToReadOnlySequenceSlow(IEnumerable<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        {
            using var enumerator = chunks.GetEnumerator();
            CreateChunks(enumerator, ref head, ref tail);
        }
    }

    /// <summary>
    /// Provides extensions for <see cref="ReadOnlyMemory{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
    extension<T>(ReadOnlyMemory<T>)
    {
        /// <summary>
        /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="chunks">The sequence of memory blocks.</param>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> Concat(params ReadOnlySpan<ReadOnlyMemory<T>> chunks)
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
    }
    
    private static void CreateChunks<T>(ReadOnlySpan<ReadOnlyMemory<T>> chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
    {
        var enumerator = chunks.GetEnumerator();
        CreateChunks(enumerator, ref head, ref tail);
    }
    
    private static void CreateChunks<T, TEnumerator>(TEnumerator chunks, ref Chunk<T>? head, ref Chunk<T>? tail)
        where TEnumerator : IEnumerator<ReadOnlyMemory<T>>, allows ref struct
    {
        while (chunks.MoveNext())
        {
            if (chunks.Current is { IsEmpty: false } segment)
            {
                Chunk<T>.AddChunk(segment, ref head, ref tail);
            }
        }
    }

    /// <summary>
    /// Constructs a sequence of characters from a collection of strings.
    /// </summary>
    /// <param name="strings">A collection of strings.</param>
    /// <returns>A sequence of characters representing concatenated strings.</returns>
    public static ReadOnlySequence<char> Concat(this IEnumerable<string?>? strings)
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
            using var enumerator = new CharMemoryEnumerator(strings);
            CreateChunks(enumerator, ref head, ref tail);
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
    public static ReadOnlySequence<char> ToSequence(this StringBuilder builder)
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
    /// Extends <see cref="ReadOnlySequence{T}"/> type.
    /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    extension<T>(in ReadOnlySequence<T> source)
    {
        /// <summary>
        /// Copies the contents from the source sequence into a destination span.
        /// </summary>
        /// <param name="src">The sequence to copy from.</param>
        /// <param name="dest">Destination memory.</param>
        /// <returns>The number of copied elements.</returns>
        public static int operator >>> (in ReadOnlySequence<T> src, Span<T> dest)
        {
            ReadOnlyMemory<T> block;
            var writer = new SpanWriter<T>(dest);

            for (var position = src.Start; src.TryGet(ref position, out block) && block.Length <= writer.FreeCapacity;)
            {
                writer += block.Span;
            }

            // copy the last segment
            writer += block.Span;
            return writer.WrittenCount;
        }

        /// <summary>
        /// Copies the contents from the source sequence into a destination span.
        /// </summary>
        /// <param name="destination">Destination memory.</param>
        /// <param name="consumed">The position within the receiver that represents the end of <paramref name="destination"/>.</param>
        /// <returns>The number of copied elements.</returns>
        public int CopyTo(Span<T> destination, out SequencePosition consumed)
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
        /// <param name="count">The size of contiguous block.</param>
        /// <param name="span">The contiguous block of elements.</param>
        /// <returns><see langword="true"/> if contiguous block of elements is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public bool TryGetBlock(int count, out ReadOnlyMemory<T> span)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            span = source.First;
            if (span.Length >= count)
            {
                span = span.Slice(0, count);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two sequences are equal by comparing the elements.
        /// </summary>
        /// <param name="other">The second sequence to compare.</param>
        /// <param name="comparer">
        /// The comparer to use when comparing elements;
        /// or <see langword="null"/> to use the <see cref="EqualityComparer{T}.Default"/>.</param>
        /// <returns><see langword="true"/> if both sequences have the same elements in the same order; otherwise, <see langword="false"/>.</returns>
        public bool SequenceEqual(ReadOnlySpan<T> other, IEqualityComparer<T>? comparer = null)
        {
            bool hasMoreSegments;
            for (var position = source.Start;
                 (hasMoreSegments = source.TryGet(ref position, out var block)) && block.Length <= other.Length;
                 other = other.Slice(block.Length))
            {
                if (!block.Span.SequenceEqual(other.Slice(0, block.Length), comparer))
                    break;
            }

            return other.IsEmpty && !hasMoreSegments;
        }

        /// <summary>
        /// Determines whether two sequences are equal by comparing the elements.
        /// </summary>
        /// <param name="other">The second sequence to compare.</param>
        /// <param name="comparer">
        /// The comparer to use when comparing elements;
        /// or <see langword="null"/> to use the <see cref="EqualityComparer{T}.Default"/>.</param>
        /// <returns><see langword="true"/> if both sequences have the same elements in the same order; otherwise, <see langword="false"/>.</returns>
        public bool SequenceEqual(in ReadOnlySequence<T> other, IEqualityComparer<T>? comparer = null)
            => (source.IsSingleSegment, other.IsSingleSegment) switch
            {
                (true, true) => source.FirstSpan.SequenceEqual(other.FirstSpan, comparer),
                (true, false) => other.SequenceEqual(source.FirstSpan, comparer),
                (false, true) => source.SequenceEqual(other.FirstSpan, comparer),
                (false, false) => source.SequenceEqualSlow(in other, comparer)
            };
        
        private bool SequenceEqualSlow(in ReadOnlySequence<T> other, IEqualityComparer<T>? comparer)
        {
            scoped var segment1 = new SequenceReaderSlim<T>(in source);
            scoped var segment2 = new SequenceReaderSlim<T>(in other);

            do
            {
                switch (segment1.Span.Length.CompareTo(segment2.Span.Length))
                {
                    case < 0 when CompareWithLargerSegment(ref segment1, ref segment2, comparer):
                    case > 0 when CompareWithLargerSegment(ref segment2, ref segment1, comparer):
                        break;
                    case 0 when segment1.Span.SequenceEqual(segment2.Span, comparer):
                        segment1.Advance();
                        segment2.Advance();
                        break;
                    default:
                        return false;
                }
            } while (segment1.HasMoreSegments && segment2.HasMoreSegments);

            return segment1.HasMoreSegments == segment2.HasMoreSegments;

            static bool CompareWithLargerSegment(ref SequenceReaderSlim<T> smaller, ref SequenceReaderSlim<T> larger,
                IEqualityComparer<T>? comparer)
            {
                Debug.Assert(smaller.Span.Length < larger.Span.Length);

                var fragment = larger.Span.Slice(0, smaller.Span.Length);
                if (!smaller.Span.SequenceEqual(fragment, comparer) || !smaller.Advance())
                    return false;

                larger.Advance(fragment.Length);
                return true;
            }
        }
    }
}

[StructLayout(LayoutKind.Auto)]
file ref struct SequenceReaderSlim<T>
{
    private readonly ref readonly ReadOnlySequence<T> sequence;
    private ReadOnlyMemory<T> block;
    private SequencePosition position;
    private bool hasMoreSegments;

    public SequenceReaderSlim(ref readonly ReadOnlySequence<T> sequence)
    {
        this.sequence = ref sequence;
        position = sequence.Start;
        hasMoreSegments = sequence.TryGet(ref position, out block);
    }

    public readonly bool HasMoreSegments => hasMoreSegments;

    public readonly ReadOnlySpan<T> Span => block.Span;

    public void Advance(int count)
    {
        Debug.Assert(count < block.Length);

        block = block.Slice(count);
    }

    public bool Advance() => hasMoreSegments = sequence.TryGet(ref position, out block);
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct CharMemoryEnumerator(IEnumerable<string?> strings) : IEnumerator<ReadOnlyMemory<char>>
{
    private readonly IEnumerator<string?> enumerator = strings.GetEnumerator();

    void IEnumerator.Reset() => enumerator.Reset();

    object? IEnumerator.Current => Current;

    public ReadOnlyMemory<char> Current => enumerator.Current.AsMemory();

    public bool MoveNext() => enumerator.MoveNext();

    public void Dispose() => enumerator.Dispose();
}