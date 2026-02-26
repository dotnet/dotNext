using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Runtime.InteropServices;

/// <summary>
/// Provides additional methods for data partitioning in parallel algorithms.
/// </summary>
public static class PartitionerExtensions
{
    /// <summary>
    /// Extends <see cref="Partitioner"/> type.
    /// </summary>
    extension(Partitioner)
    {
        /// <summary>
        /// Creates partitioner for the sequence of elements.
        /// </summary>
        /// <param name="sequence">The sequence to split.</param>
        /// <param name="splitOnSegments">
        /// <see langword="true"/> to split the sequence to the number of partitions equals to the number of the segments within the sequence;
        /// <see langword="false"/> to split the sequence dynamically to balance the workload.
        /// </param>
        /// <returns>The partitioner for the sequence.</returns>
        public static OrderablePartitioner<T> Create<T>(ReadOnlySequence<T> sequence, bool splitOnSegments = false) => sequence.IsEmpty
            ? Partitioner.Create<T>([], splitOnSegments)
            : new ReadOnlySequencePartitioner<T>(in sequence, splitOnSegments);
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