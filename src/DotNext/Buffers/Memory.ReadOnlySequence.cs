using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

using Runtime.InteropServices;

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
        /// <param name="destination">Destination memory.</param>
        /// <param name="writtenCount">The number of copied elements.</param>
        public void CopyTo(Span<T> destination, out int writtenCount)
        {
            writtenCount = 0;
            ReadOnlyMemory<T> block;

            for (var position = source.Start;
                 source.TryGet(ref position, out block) && block.Length <= destination.Length;
                 writtenCount += block.Length)
            {
                block.Span.CopyTo(destination);
                destination = destination.Slice(block.Length);
            }

            // copy the last segment
            writtenCount += block.Span >>> destination;
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
        /// Creates partitioner for the sequence of elements.
        /// </summary>
        /// <param name="splitOnSegments">
        /// <see langword="true"/> to split the sequence to the number of partitions equals to the number of the segments within the sequence;
        /// <see langword="false"/> to split the sequence dynamically to balance the workload.
        /// </param>
        /// <returns>The partitioner for the sequence.</returns>
        public OrderablePartitioner<T> CreatePartitioner(bool splitOnSegments = false)
            => source.IsEmpty ? Partitioner.Create<T>([], splitOnSegments) : new ReadOnlySequencePartitioner<T>(in source, splitOnSegments);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CharMemoryEnumerator(IEnumerable<string?> strings) : IEnumerator<ReadOnlyMemory<char>>
    {
        private readonly IEnumerator<string?> enumerator = strings.GetEnumerator();

        void IEnumerator.Reset() => enumerator.Reset();

        object? IEnumerator.Current => Current;

        public ReadOnlyMemory<char> Current => enumerator.Current.AsMemory();

        public bool MoveNext() => enumerator.MoveNext();

        public void Dispose() => enumerator.Dispose();
    }
}

file sealed class ReadOnlySequencePartitioner<T> : OrderablePartitioner<T>
{
    private sealed class SegmentProvider(in ReadOnlySequence<T> sequence) : IEnumerable<KeyValuePair<long, T>>
    {
        private readonly Lock syncRoot = new();
        private long runningIndex;
        private ReadOnlySequence<T>.Enumerator enumerator = sequence.GetEnumerator();
        
        private ReadOnlyMemory<T> NextSegment(out long startIndex)
        {
            lock (syncRoot)
            {
                startIndex = runningIndex;
                var result = enumerator.MoveNext() ? enumerator.Current : ReadOnlyMemory<T>.Empty;
                runningIndex += result.Length;
                return result;
            }
        }

        public IEnumerator<KeyValuePair<long, T>> GetEnumerator()
        {
            ReadOnlyMemory<T> segment;

            do
            {
                segment = NextSegment(out var startIndex);

                for (nint i = 0; i < segment.Length; i++, startIndex++)
                    yield return new(startIndex, Unsafe.Add(ref MemoryMarshal.GetReference(segment.Span), i));
            }
            while (!segment.IsEmpty);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private readonly ReadOnlySequence<T> sequence;

    internal ReadOnlySequencePartitioner(in ReadOnlySequence<T> sequence, bool loadBalance)
        : base(true, !loadBalance, true)
        => this.sequence = sequence;

    private static IEnumerator<KeyValuePair<long, T>> CreatePartition(long startIndex, in ReadOnlySequence<T> partition)
    {
        return CreatePartition(startIndex, partition.GetEnumerator());

        static IEnumerator<KeyValuePair<long, T>> CreatePartition(long startIndex, ReadOnlySequence<T>.Enumerator partition)
        {
            while (partition.MoveNext())
            {
                var block = partition.Current;

                for (nint i = 0; i < block.Length; i++, startIndex++)
                {
                    yield return new(startIndex, Unsafe.Add(ref MemoryMarshal.GetReference(block.Span), i));
                }
            }
        }
    }

    private void GetOrderableStaticPartitions(IEnumerator<KeyValuePair<long, T>>[] partitions)
    {
        var (quotient, remainder) = Math.DivRem(sequence.Length, partitions.Length);

        var startIndex = 0L;

        for (var i = 0; i < partitions.Length; i++)
        {
            var length = i < remainder ? quotient + 1 : quotient;
            partitions[i] = CreatePartition(startIndex, sequence.Slice(startIndex, length));
            startIndex += length;
        }
    }

    private void GetOrderableDynamicPartitions(IEnumerator<KeyValuePair<long, T>>[] partitions)
    {
        unsafe
        {
            partitions.ForEach(&CreatePartition, GetOrderableDynamicPartitions());
        }

        static void CreatePartition(ref IEnumerator<KeyValuePair<long, T>> partition, IEnumerable<KeyValuePair<long, T>> partitions)
            => partition = partitions.GetEnumerator();
    }

    public override IList<IEnumerator<KeyValuePair<long, T>>> GetOrderablePartitions(int partitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);

        var partitions = new IEnumerator<KeyValuePair<long, T>>[partitionCount];

        if (SupportsDynamicPartitions)
            GetOrderableDynamicPartitions(partitions);
        else
            GetOrderableStaticPartitions(partitions);

        return partitions;
    }

    public override IEnumerable<KeyValuePair<long, T>> GetOrderableDynamicPartitions()
        => new SegmentProvider(sequence);

    public override IList<IEnumerator<T>> GetPartitions(int partitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);

        var partitions = new IEnumerator<T>[partitionCount];
        var (quotient, remainder) = Math.DivRem(sequence.Length, partitions.Length);

        var startIndex = sequence.Start;

        for (var i = 0; i < partitions.Length; i++)
        {
            var length = i < remainder ? quotient + 1 : quotient;
            var sliced = sequence.Slice(startIndex, length);
            partitions[i] = SequenceMarshal.ToEnumerator(in sliced);
            startIndex = sliced.End;
        }

        return partitions;
    }

    public override bool SupportsDynamicPartitions => !KeysOrderedAcrossPartitions;
}