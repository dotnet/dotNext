using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace DotNext.Collections.Concurrent;

using Buffers;

partial struct PartitionedIndexPool
{
    /// <inheritdoc/>
    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (array is int[] typed)
        {
            CopyTo(typed.AsSpan(index));
        }
        else
        {
            ToArray().CopyTo(array, index);
        }
    }

    /// <inheritdoc/>
    void IProducerConsumerCollection<int>.CopyTo(int[] array, int index)
        => CopyTo(array.AsSpan(index));

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => throw new NotSupportedException();
    
    /// <inheritdoc/>
    bool IProducerConsumerCollection<int>.TryAdd(int value)
    {
        var partition = value >>> Shift;
        var subIndex = value & MaxSubIndex;

        return (uint)value <= (uint)maxValue && TryAdd(ref GetPartition(partition), subIndex);
    }

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int ICollection.Count
    {
        get
        {
            // Perf: This getter is O(N) operation
            var result = 0;
            foreach (ref readonly var partition in partitions.AsSpan())
            {
                result += BitOperations.PopCount(Volatile.Read(in partition));
            }

            return result;
        }
    }

    /// <inheritdoc/>
    public int[] ToArray()
    {
        using var writer = new BufferWriterSlim<int>(Capacity);
        for (var enumerator = GetEnumerator(); enumerator.MoveNext();)
        {
            writer.Add(enumerator.Current);
        }

        return writer.WrittenSpan.ToArray();
    }

    private static bool TryAdd(ref uint bitmask, int value)
    {
        for (uint current = bitmask, tmp;; current = tmp)
        {
            var flag = 1U << value;
            var newValue = current | flag;

            tmp = Interlocked.CompareExchange(ref bitmask, newValue, current);

            if (tmp == current)
            {
                return (current & flag) is 0U;
            }
        }
    }
}