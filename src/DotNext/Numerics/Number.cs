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
}