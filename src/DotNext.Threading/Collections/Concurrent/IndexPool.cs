using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Runtime;
using Runtime.CompilerServices;
using Threading;

/// <summary>
/// Represents a pool of integer values.
/// </summary>
/// <remarks>
/// This type is perfect to the object pooling in async scenario, because it has no thread affinity
/// in contrast to <see cref="ConcurrentBag{T}"/> class. However, its capacity is limited and doesn't
/// scale well in case of high contention.
/// This type is thread-safe.
/// </remarks>
/// <seealso cref="PartitionedIndexPool"/>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[StructLayout(LayoutKind.Auto)]
public partial struct IndexPool : ISupplier<int>, IConsumer<int>, IProducerConsumerCollection<int>, IResettable
{
    private readonly int maxValue;
    private ulong bitmask;

    /// <summary>
    /// Initializes a new pool that can return an integer value within the range [0..<see cref="MaxValue"/>].
    /// </summary>
    public IndexPool()
    {
        bitmask = ulong.MaxValue;
        maxValue = MaxValue;
    }

    /// <summary>
    /// Initializes a new pool that can return an integer within the range [0..<paramref name="maxValue"/>].
    /// </summary>
    /// <param name="maxValue">The maximum possible value to return, inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxValue"/> is less than zero;
    /// or greater than <see cref="MaxValue"/>.
    /// </exception>
    public IndexPool(int maxValue)
    {
        if ((uint)maxValue > (uint)MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue));

        bitmask = GetBitMask(this.maxValue = maxValue);
    }

    private IndexPool(ulong bitmask, int maxValue)
    {
        this.bitmask = bitmask;
        this.maxValue = maxValue;
    }

    private static ulong GetBitMask(int maxValue) => ulong.MaxValue >>> (MaxValue - maxValue);

    /// <summary>
    /// Gets or sets a value indicating that the pool is empty.
    /// </summary>
    public bool IsEmpty
    {
        readonly get => Count is 0;
        init => bitmask = value ? 0UL : GetBitMask(maxValue);
    }

    /// <summary>
    /// Gets the maximum number that can be returned by the pool.
    /// </summary>
    /// <value>Always returns <c>63</c>.</value>
    public static int MaxValue => Capacity - 1;

    /// <summary>
    /// Gets the maximum capacity of the pool.
    /// </summary>
    public static int Capacity => sizeof(ulong) * 8;

    /// <summary>
    /// Tries to peek the next available index from the pool, without acquiring it.
    /// </summary>
    /// <param name="result">The index which is greater than or equal to zero.</param>
    /// <returns><see langword="true"/> if the index is available for rent; otherwise, <see langword="false"/>.</returns>
    public readonly bool TryPeek(out int result)
        => (result = BitOperations.TrailingZeroCount(Atomic.Read(in bitmask))) <= maxValue;

    /// <summary>
    /// Returns the available index from the pool.
    /// </summary>
    /// <param name="result">The index which is greater than or equal to zero.</param>
    /// <returns><see langword="true"/> if the index is successfully rented from the pool; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Return(int)"/>
    public bool TryTake(out int result)
    {
        var current = Atomic.Read(in bitmask);
        for (ulong newValue;; current = newValue)
        {
            newValue = current & (current - 1UL); // Reset the lowest set bit, the same as BLSR instruction
            newValue = Interlocked.CompareExchange(ref bitmask, newValue, current);
            if (newValue == current)
                break;
        }

        return (result = BitOperations.TrailingZeroCount(current)) <= maxValue;
    }

    /// <summary>
    /// Returns the available index from the pool.
    /// </summary>
    /// <returns>The index which is greater than or equal to zero.</returns>
    /// <exception cref="OverflowException">There is no available index to return.</exception>
    /// <seealso cref="Return(int)"/>
    public int Take()
    {
        if (!TryTake(out var result))
            ThrowOverflowException();

        return result;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowOverflowException() => throw new OverflowException();
    }

    /// <inheritdoc/>
    int ISupplier<int>.Invoke() => Take();
    
    /// <inheritdoc cref="IFunctional.DynamicInvoke"/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
    {
        switch (count)
        {
            case 0:
                result.Mutable<int>() = Take();
                break;
            case 1:
                Return(args.Immutable<int>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    /// <summary>
    /// Returns an index previously obtained using <see cref="TryTake(out int)"/> back to the pool.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than zero or greater than the maximum
    /// value specified for this pool.</exception>
    public void Return(int value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)maxValue, nameof(value));

        Interlocked.Or(ref bitmask, 1UL << value);
    }

    /// <summary>
    /// Returns multiple indices, atomically.
    /// </summary>
    /// <param name="indices">The buffer of indices to return back to the pool.</param>
    public void Return(ReadOnlySpan<int> indices)
    {
        var newValue = 0UL;

        foreach (var index in indices)
        {
            newValue |= 1UL << index;
        }

        Interlocked.Or(ref bitmask, newValue);
    }

    /// <summary>
    /// Returns all values to the pool.
    /// </summary>
    public void Reset()
    {
        Atomic.Write(ref bitmask, ulong.MaxValue);
    }

    /// <inheritdoc/>
    void IConsumer<int>.Invoke(int value) => Return(value);

    /// <summary>
    /// Determines whether the specified index is available for rent.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is available for rent; otherwise, <see langword="false"/>.</returns>
    public readonly bool Contains(int value)
        => (uint)value <= (uint)maxValue && (Atomic.Read(in bitmask) & (1UL << value)) is not 0UL;

    /// <summary>
    /// Gets the number of available indices.
    /// </summary>
    public readonly int Count => Math.Min(GetCount(Atomic.Read(in bitmask)), maxValue + 1);

    private static int GetCount(ulong bitmask) => BitOperations.PopCount(bitmask);

    /// <summary>
    /// Takes all the indices atomically.
    /// </summary>
    /// <returns>The pool that contains all taken indices.</returns>
    public IndexPool TakeAll() => new(Interlocked.Exchange(ref bitmask, 0UL), maxValue);

    /// <summary>
    /// Copies the values from the pool to the specified destination, without removal of the values.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <returns>The number of copied elements.</returns>
    public readonly int CopyTo(Span<int> destination)
    {
        var index = 0;
        for (var enumerator = GetEnumerator(); enumerator.MoveNext(); index++)
        {
            destination[index] = enumerator.Current;
        }

        return index;
    }
}