using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext;

/// <summary>
/// Various extensions for value types.
/// </summary>
public static class ValueTypeExtensions
{
    internal static TOutput ChangeType<TInput, TOutput>(this TInput input)
        where TInput : struct, IConvertible
        where TOutput : struct, IConvertible
        => (TOutput)input.ToType(typeof(TOutput), InvariantCulture);

    /// <summary>
    /// Attempts to get value from nullable container.
    /// </summary>
    /// <typeparam name="T">The underlying value type of the nullable type.</typeparam>
    /// <param name="nullable">Nullable value.</param>
    /// <param name="value">Underlying value.</param>
    /// <returns><see langword="true"/> if <paramref name="nullable"/> is not <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetValue<T>(this T? nullable, out T value)
        where T : struct
    {
        value = nullable.GetValueOrDefault();
        return nullable.HasValue;
    }

    /// <summary>
    /// Converts <see cref="bool"/> into <see cref="int"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see cref="int"/> representation of <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(this bool value)
    {
        Push(value);
        return Return<int>();
    }

    /// <summary>
    /// Converts <see cref="bool"/> into <see cref="byte"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see cref="byte"/> representation of <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this bool value)
    {
        Push(value);
        Conv_U1();
        return Return<byte>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sbyte ToSByte(this bool value)
    {
        Push(value);
        Conv_I1();
        return Return<sbyte>();
    }

    /// <summary>
    /// Converts <see cref="int"/> into <see cref="bool"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see langword="true"/> if <c>value != 0</c>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ToBoolean(this int value) => value is not 0;

    /// <summary>
    /// Normalizes value in the specified range.
    /// </summary>
    /// <typeparam name="T">The type of the value to be normalized.</typeparam>
    /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
    /// <param name="min">The lower bound of the value.</param>
    /// <param name="max">The upper bound of the value.</param>
    /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
    [CLSCompliant(false)]
    public static float NormalizeToSingle<T>(this T value, T min, T max)
        where T : struct, IConvertible, IComparable<T>
    {
        var v = value.ToSingle(InvariantCulture);
        return value.CompareTo(default) > 0 ?
            v / max.ToSingle(InvariantCulture) :
            -v / min.ToSingle(InvariantCulture);
    }

    /// <summary>
    /// Normalizes value in the specified range.
    /// </summary>
    /// <typeparam name="T">The type of the value to be normalized.</typeparam>
    /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
    /// <param name="min">The lower bound of the value.</param>
    /// <param name="max">The upper bound of the value.</param>
    /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
    [CLSCompliant(false)]
    public static double NormalizeToDouble<T>(this T value, T min, T max)
        where T : struct, IConvertible, IComparable<T>
    {
        var v = value.ToDouble(InvariantCulture);
        return value.CompareTo(default) > 0 ?
            v / max.ToDouble(InvariantCulture) :
            -v / min.ToDouble(InvariantCulture);
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