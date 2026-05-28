using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

using Runtime;
using Runtime.CompilerServices;

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
            => source.Compare<T, bool, SpanEqualityComparer<T>>(ref other, new(comparer), out var hasMoreSegments)
               && other.IsEmpty
               && !hasMoreSegments;

        /// <summary>
        /// Determines whether two sequences are equal by comparing the elements.
        /// </summary>
        /// <param name="other">The second sequence to compare.</param>
        /// <param name="comparer">
        /// The comparer to use when comparing elements;
        /// or <see langword="null"/> to use the <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
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

            return segment1.Compare<T, bool, SpanEqualityComparer<T>>(ref segment2, new(comparer))
                   && segment1.HasMoreSegments == segment2.HasMoreSegments;
        }

        /// <summary>
        /// Determines the relative order of the sequences being compared by comparing the elements.
        /// </summary>
        /// <param name="other">The second sequence to compare.</param>
        /// <param name="comparer">
        /// The comparer to use when comparing elements;
        /// or <see langword="null"/> to use the <see cref="Comparer{T}.Default"/>.
        /// </param>
        /// <returns>A signed integer that indicates the relative order.</returns>
        public int SequenceCompareTo(ReadOnlySpan<T> other, IComparer<T>? comparer = null)
        {
            var cmp = source.SequenceCompareTo(other, new SpanComparer<T>(comparer), out var firstNotEmpty, out var secondNotEmpty);
            if (cmp is 0)
                cmp = firstNotEmpty.CompareTo(secondNotEmpty);

            return cmp;
        }

        /// <summary>
        /// Determines the relative order of the sequences being compared by comparing the elements.
        /// </summary>
        /// <param name="other">The second sequence to compare.</param>
        /// <param name="comparer">
        /// The comparer to use when comparing elements;
        /// or <see langword="null"/> to use the <see cref="Comparer{T}.Default"/>.
        /// </param>
        /// <returns>A signed integer that indicates the relative order.</returns>
        public int SequenceCompareTo(in ReadOnlySequence<T> other, IComparer<T>? comparer = null)
        {
            int cmp;
            bool firstNotEmpty;
            bool secondNotEmpty;
            switch (source.IsSingleSegment, other.IsSingleSegment)
            {
                case (true, true):
                    cmp = source.FirstSpan.SequenceCompareTo(other.FirstSpan, comparer);
                    firstNotEmpty = secondNotEmpty = false;
                    break;
                case (false, true):
                    cmp = source.SequenceCompareTo(other.FirstSpan, new SpanComparer<T>(comparer), out firstNotEmpty, out secondNotEmpty);
                    break;
                case (true, false):
                    cmp = other.SequenceCompareTo(source.FirstSpan, new ReversedSpanComparer<T, int, SpanComparer<T>>(new(comparer)),
                        out secondNotEmpty, out firstNotEmpty);
                    
                    break;
                case (false, false):
                    cmp = source.SequenceCompareTo(in other, comparer, out firstNotEmpty, out secondNotEmpty);
                    break;
            }

            if (cmp is 0)
                cmp = firstNotEmpty.CompareTo(secondNotEmpty);

            return cmp;
        }
    }
}

file static class ComparisonHelpers
{
    public static int SequenceCompareTo<T, TComparer>(this in ReadOnlySequence<T> source, ReadOnlySpan<T> other,
        scoped TComparer comparer, out bool firstNotEmpty, out bool secondNotEmpty)
        where TComparer : struct, ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, int>, IComparisonResultSupport<int>, allows ref struct
    {
        var cmp = source.Compare<T, int, TComparer>(
            ref other,
            comparer,
            out firstNotEmpty);

        secondNotEmpty = other.Length > 0;
        return cmp;
    }

    public static int SequenceCompareTo<T>(this in ReadOnlySequence<T> source, in ReadOnlySequence<T> other,
        IComparer<T>? comparer, out bool firstNotEmpty, out bool secondNotEmpty)
    {
        scoped var segment1 = new SequenceReaderSlim<T>(in source);
        scoped var segment2 = new SequenceReaderSlim<T>(in other);

        var cmp = segment1.Compare<T, int, SpanComparer<T>>(ref segment2, new(comparer));
        firstNotEmpty = segment1.HasMoreSegments;
        secondNotEmpty = segment2.HasMoreSegments;
        return cmp;
    }
    
    public static TResult Compare<T, TResult, TComparer>(this in ReadOnlySequence<T> source, ref ReadOnlySpan<T> other, scoped TComparer comparer, out bool hasMoreSegments)
        where TComparer : struct, ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>, IComparisonResultSupport<TResult>, allows ref struct
    {
        var result = TComparer.Equality;
        for (var position = source.Start;
             (hasMoreSegments = source.TryGet(ref position, out var block)) && block.Length <= other.Length;
             other = other.Slice(block.Length))
        {
            result = comparer.Invoke(block.Span, other.Slice(0, block.Length));
            if (!TComparer.MeansEquality(result))
                break;
        }

        return result;
    }

    public static TResult Compare<T, TResult, TComparer>(this ref SequenceReaderSlim<T> segment,
        ref SequenceReaderSlim<T> other, TComparer comparer)
        where TComparer : struct, ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>, IComparisonResultSupport<TResult>, allows ref struct
    {
        var result = TComparer.Equality;
        do
        {
            switch (segment.Span.Length.CompareTo(other.Span.Length))
            {
                case < 0 when TComparer.MeansEquality(result = segment.CompareWithLargerSegment<T, TResult, TComparer>(ref other, comparer)):
                case > 0 when TComparer.MeansEquality(result =
                    other.CompareWithLargerSegment<T, TResult, ReversedSpanComparer<T, TResult, TComparer>>(ref segment, new(comparer))):
                    continue;
                case 0 when TComparer.MeansEquality(result = comparer.Invoke(segment.Span, other.Span)):
                    segment.Advance();
                    other.Advance();
                    continue;
            }

            break;
        } while (segment.HasMoreSegments && other.HasMoreSegments);

        return result;
    }

    private static TResult CompareWithLargerSegment<T, TResult, TComparer>(this ref SequenceReaderSlim<T> segment,
        ref SequenceReaderSlim<T> other, TComparer comparer)
        where TComparer : struct, ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>, IComparisonResultSupport<TResult>, allows ref struct
    {
        Debug.Assert(segment.Span.Length < other.Span.Length);

        var fragment = other.Span.Slice(0, segment.Span.Length);
        var cmp = comparer.Invoke(segment.Span, fragment);
        if (!TComparer.MeansEquality(cmp) || !segment.Advance())
            return cmp;

        other.Advance(fragment.Length);
        return cmp;
    }
}

file interface IComparisonResultSupport<TResult>
{
    public static abstract bool MeansEquality(TResult result);
    
    public static abstract TResult Equality { get; }
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct ReversedSpanComparer<T, TResult, TComparer>(TComparer comparer) :
    ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>,
    IComparisonResultSupport<TResult>
    where TComparer : struct, ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>, IComparisonResultSupport<TResult>, allows ref struct
{
    private readonly TComparer comparer = comparer;

    TResult ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, TResult>.Invoke(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
    {
        var copy = comparer;
        return copy.Invoke(y, x);
    }

    static bool IComparisonResultSupport<TResult>.MeansEquality(TResult result) => TComparer.MeansEquality(result);

    static TResult IComparisonResultSupport<TResult>.Equality => TComparer.Equality;

    void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        => throw new NotSupportedException();
}

[StructLayout(LayoutKind.Auto)]
file readonly struct SpanComparer<T>(IComparer<T>? comparer) : ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, int>, IComparisonResultSupport<int>
{
    int ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, int>.Invoke(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        => x.SequenceCompareTo(y, comparer);

    static bool IComparisonResultSupport<int>.MeansEquality(int result) => result is 0;

    static int IComparisonResultSupport<int>.Equality => 0;
}

[StructLayout(LayoutKind.Auto)]
file readonly struct SpanEqualityComparer<T>(IEqualityComparer<T>? comparer)
    : ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, bool>, IComparisonResultSupport<bool>
{
    bool ISupplier<ReadOnlySpan<T>, ReadOnlySpan<T>, bool>.Invoke(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        => x.SequenceEqual(y, comparer);

    static bool IComparisonResultSupport<bool>.MeansEquality(bool result) => result;
    
    static bool IComparisonResultSupport<bool>.Equality => true;
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

    object IEnumerator.Current => Current;

    public ReadOnlyMemory<char> Current => enumerator.Current.AsMemory();

    public bool MoveNext() => enumerator.MoveNext();

    public void Dispose() => enumerator.Dispose();
}