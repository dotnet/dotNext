using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

/// <summary>
/// Represents a pool of integer values.
/// </summary>
/// <remarks>
/// This type is thread-safe.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[StructLayout(LayoutKind.Auto)]
public struct IndexPool : ISupplier<int>, IConsumer<int>, IReadOnlyCollection<int>
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

        bitmask = ulong.MaxValue;
        this.maxValue = maxValue;
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
        => (result = BitOperations.TrailingZeroCount(Volatile.Read(in bitmask))) <= maxValue;

    /// <summary>
    /// Returns the available index from the pool.
    /// </summary>
    /// <param name="result">The index which is greater than or equal to zero.</param>
    /// <returns><see langword="true"/> if the index is successfully rented from the pool; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Return(int)"/>
    public bool TryTake(out int result)
    {
        return TryTake(ref bitmask, maxValue, out result);

        static bool TryTake(ref ulong bitmask, int maxValue, out int result)
        {
            var current = Volatile.Read(in bitmask);
            for (ulong newValue; ; current = newValue)
            {
                newValue = current & (current - 1UL); // Reset lowest set bit, the same as BLSR instruction
                newValue = Interlocked.CompareExchange(ref bitmask, newValue, current);
                if (newValue == current)
                    break;
            }

            return (result = BitOperations.TrailingZeroCount(current)) <= maxValue;
        }
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

    /// <summary>
    /// Takes all available indicies, atomically.
    /// </summary>
    /// <param name="indicies">
    /// The buffer to be modified with the indicies taken from the pool.
    /// The size of the buffer should not be less than <see cref="Capacity"/>.
    /// </param>
    /// <returns>The number of indicies written to the buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="indicies"/> is too small to place indicies.</exception>
    /// <seealso cref="Return(ReadOnlySpan{int})"/>
    public int Take(Span<int> indicies)
    {
        if (indicies.Length < Capacity)
            throw new ArgumentOutOfRangeException(nameof(indicies));

        var oldValue = Interlocked.Exchange(ref bitmask, 0UL);
        var bufferOffset = 0;

        for (int bitPosition = 0; bitPosition < Capacity; bitPosition++)
        {
            if (Contains(oldValue, bitPosition))
            {
                indicies[bufferOffset++] = bitPosition;
            }
        }

        return bufferOffset;
    }

    /// <inheritdoc/>
    int ISupplier<int>.Invoke() => Take();

    /// <summary>
    /// Returns an index previously obtained using <see cref="TryTake(out int)"/> back to the pool.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than zero or greater than the maximum
    /// value specified for this pool.</exception>
    public void Return(int value)
    {
        if ((uint)value > (uint)maxValue)
            ThrowArgumentOutOfRangeException();

        Interlocked.Or(ref bitmask, 1UL << value);

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowArgumentOutOfRangeException()
            => throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Returns multiple indicies, atomically.
    /// </summary>
    /// <param name="indicies">The buffer of indicies to return back to the pool.</param>
    public void Return(ReadOnlySpan<int> indicies)
    {
        var newValue = 0UL;

        foreach (var index in indicies)
        {
            newValue |= 1UL << index;
        }

        Interlocked.Or(ref bitmask, newValue);
    }

    /// <inheritdoc/>
    void IConsumer<int>.Invoke(int value) => Return(value);

    /// <summary>
    /// Determines whether the specified index is available for rent.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is available for rent; otherwise, <see langword="false"/>.</returns>
    public readonly bool Contains(int value)
        => (uint)value <= (uint)maxValue && Contains(Volatile.Read(in bitmask), value);

    private static bool Contains(ulong bitmask, int index)
        => (bitmask & (1UL << index)) is not 0UL;

    /// <summary>
    /// Gets the number of available indicies.
    /// </summary>
    public readonly int Count => Math.Min(BitOperations.PopCount(bitmask), maxValue + 1);

    /// <summary>
    /// Gets an enumerator over available indicies in the pool.
    /// </summary>
    /// <returns>The enumerator over available indicies in this pool.</returns>
    public readonly Enumerator GetEnumerator() => new(Volatile.Read(in bitmask), maxValue);

    /// <inheritdoc/>
    readonly IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator().AsClassicEnumerator();

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator().AsClassicEnumerator();

    /// <summary>
    /// Represents an enumerator over available indicies in the pool.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private readonly ulong bitmask;
        private readonly int maxValue;
        private int current;

        internal Enumerator(ulong bitmask, int maxValue)
        {
            this.bitmask = bitmask;
            this.maxValue = maxValue;
            current = -1;
        }

        /// <summary>
        /// Gets the current index.
        /// </summary>
        public readonly int Current => current;

        /// <summary>
        /// Advances to the next available index.
        /// </summary>
        /// <returns><see langword="true"/> if enumerator advanced successfully; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            while (++current <= maxValue)
            {
                if (Contains(bitmask, current))
                {
                    return true;
                }
            }

            return false;
        }

        internal IEnumerator<int> AsClassicEnumerator()
        {
            while (MoveNext())
                yield return Current;
        }
    }
}