using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

/// <summary>
/// Represents Generic Math extensions.
/// </summary>
public static partial class Number
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
        where T : INumberBase<T>
        => T.IsNegative(-T.One);

    /// <summary>
    /// Gets maximum number of bytes that can be used by <typeparamref name="T"/> type
    /// when encoded in little-endian or big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type to check.</typeparam>
    /// <returns>The maximum numbers bytes that can be occupied by the value of <typeparamref name="T"/>.</returns>
    public static int GetMaxByteCount<T>()
        where T : IBinaryInteger<T>
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
    /// Determines whether the specified value is a prime number.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a prime number; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative or zero.</exception>
    public static bool IsPrime<T>(this T value)
        where T : struct, IBinaryNumber<T>, ISignedNumber<T>, IShiftOperators<T, int, T>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

        if (value == T.One)
            return false;

        var two = T.One << 1;

        if ((value & T.One) != T.Zero)
        {
            for (T divisor = two + T.One, limit = Sqrt(value); divisor <= limit; divisor += two)
            {
                if ((value % divisor) == T.Zero)
                    return false;
            }

            return true;
        }

        return value == two;

        // https://math.stackexchange.com/questions/2469446/what-is-a-fast-algorithm-for-finding-the-integer-square-root/4674078#4674078
        static T Sqrt(T value)
        {
            var log2x = T.Log2(value) - T.One;
            var log2y = int.CreateChecked(log2x >> 1);

            var y = T.One << log2y;
            var y_squared = T.One << (2 * log2y);

            var sqr_diff = value - y_squared;

            // Perform lerp between powers of four
            y += (sqr_diff / (T.One + T.One + T.One)) >> log2y;

            // The estimate is probably too low, refine it upward
            y_squared = y * y;
            sqr_diff = value - y_squared;

            y += sqr_diff / (y << 1);

            // The estimate may be too high. If so, refine it downward
            y_squared = y * y;
            sqr_diff = value - y_squared;
            if (sqr_diff >= T.Zero)
            {
                return y;
            }

            y -= (-sqr_diff / (y << 1)) + T.One;

            // The estimate may still be 1 too high
            y_squared = y * y;
            sqr_diff = value - y_squared;
            if (sqr_diff < T.Zero)
            {
                --y;
            }

            return y;
        }
    }

    /// <summary>
    /// Gets a prime number which is greater than the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="lowerBound">The value which is smaller than the requested prime number.</param>
    /// <param name="cachedPrimes">The table with cached prime numbers sorted in ascending order.</param>
    /// <returns>The prime number which is greater than <paramref name="lowerBound"/>.</returns>
    /// <exception cref="OverflowException">There is no prime number that is greater than <paramref name="lowerBound"/> and less than <see cref="IMinMaxValue{T}.MaxValue"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T GetPrime<T>(T lowerBound, ReadOnlySpan<T> cachedPrimes = default)
        where T : struct, IBinaryNumber<T>, ISignedNumber<T>, IMinMaxValue<T>, IShiftOperators<T, int, T>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lowerBound);

        if (TryGetFromTable(cachedPrimes, lowerBound, out T result))
            return result;

        //outside predefined table
        for (result = lowerBound | T.One; result < T.MaxValue; result += T.One + T.One)
        {
            if (IsPrime(result))
                return result;
        }

        throw new OverflowException();

        static bool TryGetFromTable(ReadOnlySpan<T> cachedPrimes, T value, out T result)
        {
            var low = 0;
            for (var high = cachedPrimes.Length; low < high;)
            {
                var mid = (low + high) / 2;
                result = cachedPrimes[mid];
                var cmp = result.CompareTo(value);
                if (cmp > 0)
                    high = mid;
                else
                    low = mid + 1;
            }

            bool success;
            result = (success = low < cachedPrimes.Length)
                ? T.CreateChecked(cachedPrimes[low])
                : default;

            return success;
        }
    }

    /// <summary>
    /// Rounds up the value to the multiple of the specified multiplier.
    /// </summary>
    /// <param name="value">The value to round up.</param>
    /// <param name="multiplier">The multiplier.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    /// <returns><see cref="value"/> rounded up to the multiple of <see cref="multiplier"/>.</returns>
    public static T RoundUp<T>(this T value, T multiplier)
        where T : struct, IUnsignedNumber<T>, IModulusOperators<T, T, T>
    {
        var other = RoundDown(value, multiplier);
        return other == value ? value : checked(other + multiplier);
    }

    /// <summary>
    /// Rounds down the value to the multiple of the specified multiplier.
    /// </summary>
    /// <param name="value">The value to round down.</param>
    /// <param name="multiplier">The multiplier.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    /// <returns><see cref="value"/> rounded down to the multiple of <see cref="multiplier"/>.</returns>
    public static T RoundDown<T>(this T value, T multiplier)
        where T : struct, IUnsignedNumber<T>, IModulusOperators<T, T, T>
        => value - value % multiplier;
}
