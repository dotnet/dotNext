using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Numerics;
using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents partitioned index pool that has the capacity larger than <see cref="IndexPool.Capacity"/>.
/// </summary>
/// <remarks>
/// This type is perfect to the object pooling in async scenario, because it has no thread affinity
/// in contrast to <see cref="ConcurrentBag{T}"/> class. Also, it tries to reduce the contention by
/// distributing the callers between different partitions. The contention can happen if two callers
/// trying to take the index from the same partition (bucket). However, the contention itself is lock-free.
/// This type is thread-safe.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly partial struct PartitionedIndexPool : IProducerConsumerCollection<int>, ISupplier<int>, IConsumer<int>
{
    private const int Shift = 5; // because 2^5 = 32, which is maximum capacity
    private const int PartitionCapacity = 1 << Shift;
    private const int MaxSubIndex = PartitionCapacity - 1;
    
    private readonly FastMod fastMod;
    private readonly uint[] partitions;
    private readonly int maxValue;
    private readonly PartitioningStrategy strategy;
    
    /// <summary>
    /// Creates a new pool.
    /// </summary>
    /// <param name="partitionCount">The desired number of partitions.</param>
    /// <param name="strategy">The partitioning strategy.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="partitionCount"/> is negative, or zero, or too large.</exception>
    public PartitionedIndexPool(int partitionCount, PartitioningStrategy strategy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentNullException.ThrowIfNull(strategy);

        this.strategy = strategy;
        try
        {
            partitionCount = PrimeNumber.GetPrime(partitionCount);
            maxValue = checked(partitionCount * PartitionCapacity) - 1;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionCount));
        }

        fastMod = new((uint)partitionCount);
        Array.Fill(partitions = new uint[partitionCount], uint.MaxValue);
    }

    /// <summary>
    /// Gets the maximum number of indices that can be returned by this pool.
    /// </summary>
    /// <remarks>
    /// You can use this property to initialize the size of the storge for pooled objects.
    /// </remarks>
    public int Capacity => maxValue + 1;

    private ref uint GetPartition(out int partition)
        => ref GetPartition(partition = (int)fastMod.GetRemainder((uint)strategy.GetPartition()));

    private ref uint GetPartition(int partition)
    {
        Debug.Assert((uint)partition < (uint)partitions.Length);
        
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(partitions), partition);
    }

    /// <summary>
    /// Tries to take the integer from the bucket that belongs to the current managed thread.
    /// </summary>
    /// <param name="result">The result of the operation.</param>
    /// <returns><see langword="true"/> if the index is successfully rented from the pool; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Return(int)"/>
    public bool TryTake(out int result)
    {
        var subIndex = Take(ref GetPartition(out var partition));
        result = (partition << Shift) + subIndex;

        return subIndex <= MaxSubIndex;
    }

    private static int Take(ref uint bitmask)
    {
        var current = bitmask;
        for (uint newValue;; current = newValue)
        {
            newValue = current & (current - 1U); // Reset the lowest set bit, the same as BLSR instruction
            newValue = Interlocked.CompareExchange(ref bitmask, newValue, current);
            if (newValue == current)
                break;
        }

        return BitOperations.TrailingZeroCount(current);
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
    /// Returns the value back to the pool.
    /// </summary>
    /// <param name="value">The value to return.</param>
    public void Return(int value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)maxValue, nameof(value));

        var partition = value >>> Shift;
        var subIndex = value & MaxSubIndex;

        Interlocked.Or(ref GetPartition(partition), 1U << subIndex);
    }
    
    /// <inheritdoc/>
    void IConsumer<int>.Invoke(int value) => Return(value);

    /// <summary>
    /// Determines whether the specified index is available for rent.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is available for rent; otherwise, <see langword="false"/>.</returns>
    public bool Contains(int value)
    {
        if ((uint)value <= (uint)maxValue)
        {
            var partition = value >>> Shift;
            var subIndex = value & MaxSubIndex;
            return Contains(in GetPartition(partition), subIndex);
        }

        return false;
    }

    private static bool Contains(ref readonly uint bitmask, int value)
        => (Volatile.Read(in bitmask) & (1U << value)) is not 0U;
    
    private int CopyTo(Span<int> destination)
    {
        var index = 0;
        for (var enumerator = GetEnumerator(); enumerator.MoveNext(); index++)
        {
            destination[index] = enumerator.Current;
        }

        return index;
    }
}