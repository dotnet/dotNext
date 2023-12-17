using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers.Binary;

/// <summary>
/// Provides various binary transformations.
/// </summary>
public static partial class BinaryTransformations
{
    private interface IUnaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T value);
    }

    private interface IBinaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T x, T y);
    }

    /// <summary>
    /// Reverse bytes in the specified value of blittable type.
    /// </summary>
    /// <typeparam name="T">Blittable type.</typeparam>
    /// <param name="value">The value which bytes should be reversed.</param>
    public static void Reverse<T>(ref T value)
        where T : unmanaged
        => Span.AsBytes(ref value).Reverse();

    /// <summary>
    /// Gets little-endian representation of a value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <paramref name="value"/> in little-endian format.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LittleEndian<T> AsLittleEndian<T>(this T value)
        where T : unmanaged, IBinaryInteger<T>
        => new() { Value = value };

    /// <summary>
    /// Gets big-endian representation of a value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <paramref name="value"/> in little-endian format.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigEndian<T> AsBigEndian<T>(this T value)
        where T : unmanaged, IBinaryInteger<T>
        => new() { Value = value };
}