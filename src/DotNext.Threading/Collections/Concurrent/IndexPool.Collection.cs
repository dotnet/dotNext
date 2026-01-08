using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotNext.Collections.Concurrent;

using Threading;

partial struct IndexPool
{
    /// <inheritdoc/>
    readonly void ICollection.CopyTo(Array array, int index)
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
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    readonly bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    readonly object ICollection.SyncRoot => throw new NotSupportedException();

    /// <inheritdoc/>
    readonly void IProducerConsumerCollection<int>.CopyTo(int[] array, int index)
        => CopyTo(array.AsSpan(index));

    /// <inheritdoc/>
    public readonly int[] ToArray()
    {
        var enumerator = GetEnumerator();
        var array = new int[enumerator.RemainingCount];

        for (var index = 0; enumerator.MoveNext(); index++)
        {
            array[index] = enumerator.Current;
        }

        return array;
    }

    /// <inheritdoc/>
    bool IProducerConsumerCollection<int>.TryAdd(int value)
        => (uint)value <= (uint)maxValue && TryAdd(ref bitmask, value);

    private static bool TryAdd(ref ulong bitmask, int value)
    {
        for (ulong current = Atomic.Read(in bitmask), tmp;; current = tmp)
        {
            var flag = 1UL << value;
            var newValue = current | flag;

            tmp = Interlocked.CompareExchange(ref bitmask, newValue, current);

            if (tmp == current)
            {
                return (current & flag) is 0UL;
            }
        }
    }
}