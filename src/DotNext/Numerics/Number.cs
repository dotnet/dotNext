using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

/// <summary>
/// Represents Generic Math extensions.
/// </summary>
public static class Number
{
    /// <summary>
    /// Determines whether the specified numeric type is signed.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>
    /// <see langword="true"/> if <typeparamref name="T"/> is a signed numeric type;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsSigned<T>()
        where T : notnull, INumberBase<T>
        => T.IsNegative(-T.One);

    /// <summary>
    /// Gets maximum number of bytes that can be used by <typeparamref name="T"/> type
    /// when encoded in little-endian or big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type to check.</typeparam>
    /// <returns>The maximum numbers bytes that can be occupied by the value of <typeparamref name="T"/>.</returns>
    public static int GetMaxByteCount<T>()
        where T : notnull, IBinaryInteger<T>
        => typeof(T).IsPrimitive ? Unsafe.SizeOf<T>() : T.AllBitsSet.GetByteCount();

    /// <summary>
    /// Normalizes value in the specified range.
    /// </summary>
    /// <typeparam name="TInput">The type of the input value and bounds.</typeparam>
    /// <typeparam name="TOutput">The type of normalized value.</typeparam>
    /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
    /// <param name="min">The lower bound of the value.</param>
    /// <param name="max">The upper bound of the value.</param>
    /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
    public static TOutput Normalize<TInput, TOutput>(this TInput value, TInput min, TInput max)
        where TInput : struct, INumberBase<TInput>, IComparisonOperators<TInput, TInput, bool>
        where TOutput : struct, IFloatingPoint<TOutput>
    {
        var x = TOutput.CreateChecked(value);
        TInput y;
        if (value > TInput.Zero)
        {
            y = max;
        }
        else
        {
            y = min;
            x = -x;
        }

        return x / TOutput.CreateChecked(y);
    }

    /// <summary>
    /// Normalizes 64-bit unsigned integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    [CLSCompliant(false)]
    public static double Normalize(this ulong value)
    {
        const ulong fraction = ulong.MaxValue >> (64 - 53);
        const ulong exponent = 1UL << 53;
        return BitConverter.UInt64BitsToDouble(fraction & value) / BitConverter.UInt64BitsToDouble(exponent);
    }

    /// <summary>
    /// Normalizes 64-bit signed integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    public static double Normalize(this long value)
        => Normalize(unchecked((ulong)value));

    /// <summary>
    /// Normalizes 32-bit unsigned integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    [CLSCompliant(false)]
    public static float Normalize(this uint value)
    {
        const uint fraction = uint.MaxValue >> (32 - 24);
        const uint exponent = 1U << 24;
        return BitConverter.UInt32BitsToSingle(fraction & value) / BitConverter.UInt32BitsToSingle(exponent);
    }

    /// <summary>
    /// Normalizes 32-bit signed integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    public static float Normalize(this int value)
        => Normalize(unchecked((uint)value));

    /// <summary>
    /// Gets enumerator over the specified range of elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the range.</typeparam>
    /// <typeparam name="TLowerBound">The type of lower bound.</typeparam>
    /// <typeparam name="TUpperBound">The type of upper bound.</typeparam>
    /// <param name="range">The range of elements.</param>
    /// <returns>The enumerator over elements in the range.</returns>
    public static RangeEnumerator<T> GetEnumerator<T, TLowerBound, TUpperBound>(this ValueTuple<TLowerBound, TUpperBound> range)
        where T : notnull, IBinaryInteger<T>
        where TLowerBound : notnull, IFiniteRangeEndpoint<T>
        where TUpperBound : notnull, IFiniteRangeEndpoint<T>
    {
        var (minValue, maxValue) = GetMinMaxValues<T, TLowerBound, TUpperBound>(range.Item1, range.Item2);

        return minValue < maxValue
            ? default
            : new(minValue, maxValue);
    }

    internal static (T MinValue, T MaxValue) GetMinMaxValues<T, TLowerBound, TUpperBound>(TLowerBound lowerBound, TUpperBound upperBound)
        where T : notnull, IBinaryInteger<T>
        where TLowerBound : notnull, IFiniteRangeEndpoint<T>
        where TUpperBound : notnull, IFiniteRangeEndpoint<T>
    {
        var minValue = lowerBound.IsOnRight(lowerBound.Value)
            ? lowerBound.Value
            : lowerBound.Value + T.One;

        var maxValue = upperBound.IsOnLeft(upperBound.Value)
            ? upperBound.Value
            : upperBound.Value - T.One;

        return (minValue, maxValue);
    }

    /// <summary>
    /// Represents an enumerator over range elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the range.</typeparam>
    public struct RangeEnumerator<T>
        where T : notnull, IBinaryInteger<T>
    {
        private readonly T upperBound;
        private T current;
        private bool notInitialized;

        internal RangeEnumerator(T lowerBound, T upperBound)
        {
            current = lowerBound;
            this.upperBound = upperBound;
            notInitialized = true;
        }

        /// <summary>
        /// Gets the currently enumerating element.
        /// </summary>
        public readonly T Current => current;

        /// <summary>
        /// Tries to advance enumerator to the next element in the range.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if this enumerator advances to the next element in the range;
        /// <see langword="false"/> if this enumerator reaches the end of the range.
        /// </returns>
        public bool MoveNext()
        {
            if (notInitialized)
            {
                notInitialized = false;
            }
            else if (current == upperBound)
            {
                return false;
            }
            else
            {
                current++;
            }

            return true;
        }
    }
}