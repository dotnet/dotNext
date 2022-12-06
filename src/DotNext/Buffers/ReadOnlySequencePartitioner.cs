using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Sequence = Collections.Generic.Sequence;

internal sealed class ReadOnlySequencePartitioner<T> : OrderablePartitioner<T>
{
    private sealed class SegmentProvider : IEnumerable<KeyValuePair<long, T>>
    {
        private long runningIndex;
        private ReadOnlySequence<T>.Enumerator enumerator;

        internal SegmentProvider(ReadOnlySequence<T> sequence)
        {
            enumerator = sequence.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ReadOnlyMemory<T> NextSegment(out long startIndex)
        {
            startIndex = runningIndex;
            var result = enumerator.MoveNext() ? enumerator.Current : ReadOnlyMemory<T>.Empty;
            runningIndex += result.Length;
            return result;
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
        if (partitionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

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
        if (partitionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

        var partitions = new IEnumerator<T>[partitionCount];
        var (quotient, remainder) = Math.DivRem(sequence.Length, partitions.Length);

        var startIndex = sequence.Start;

        for (var i = 0; i < partitions.Length; i++)
        {
            var length = i < remainder ? quotient + 1 : quotient;
            var sliced = sequence.Slice(startIndex, length);
            partitions[i] = Sequence.ToEnumerator(in sliced);
            startIndex = sliced.End;
        }

        return partitions;
    }

    public override bool SupportsDynamicPartitions => !KeysOrderedAcrossPartitions;
}

/// <summary>
/// Represents factory of <see cref="Partitioner{TSource}"/> objects for <see cref="ReadOnlySequence{T}"/>.
/// </summary>
public static class ReadOnlySequencePartitioner
{
    /// <summary>
    /// Creates partitioner for the sequence of elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="sequence">The sequence of elements.</param>
    /// <param name="splitOnSegments">
    /// <see langword="true"/> to split the sequence to the number of partitions equals to the number of the segments within the sequence;
    /// <see langword="false"/> to split the sequence dynamically to balance the workload.
    /// </param>
    /// <returns>The partitioner for the sequence.</returns>
    public static OrderablePartitioner<T> CreatePartitioner<T>(this in ReadOnlySequence<T> sequence, bool splitOnSegments = false)
        => sequence.IsEmpty ? Partitioner.Create<T>(Array.Empty<T>(), splitOnSegments) : new ReadOnlySequencePartitioner<T>(in sequence, splitOnSegments);
}