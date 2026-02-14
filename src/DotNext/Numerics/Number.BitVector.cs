using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Numerics;

public static partial class Number
{
    /// <summary>
    /// Gets a value indicating that the specified bit is set.
    /// </summary>
    /// <param name="number">The number to inspect.</param>
    /// <param name="position">The position of the bit within <paramref name="number"/>.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    /// <returns><see langword="true"/> if the bit at <paramref name="position"/> is set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public static bool IsBitSet<T>(this T number, int position)
        where T : struct, INumber<T>, IBitwiseOperators<T, T, T>, IShiftOperators<T, int, T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        return (number & (T.One << position)) != T.Zero;
    }

    /// <summary>
    /// Sets the bit at the specified position.
    /// </summary>
    /// <param name="number">The number to modify.</param>
    /// <param name="position">The position of the bit to set.</param>
    /// <param name="value">The bit value.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    /// <returns>A modified number.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public static T SetBit<T>(this T number, int position, bool value)
        where T : struct, INumber<T>, IBitwiseOperators<T, T, T>, IShiftOperators<T, int, T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        var bit = T.One << position;
        return value
            ? number | bit
            : number & ~bit;
    }

    /// <summary>
    /// Converts bit vector to a value of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="bits">A vector of bits.</param>
    /// <returns>A value of type <typeparamref name="TResult"/> restored from the vector of bits.</returns>
    public static TResult FromBits<TResult>(this ReadOnlySpan<bool> bits)
        where TResult : struct, IBinaryInteger<TResult>
    {
        var result = TResult.Zero;
        
        for (var position = 0; position < bits.Length; position++)
        {
            if (bits[position])
                result |= TResult.One << position;
        }

        return result;
    }

    /// <summary>
    /// Converts a value to a set of bits.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="bits">A buffer to be modified.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bits"/> has not enough length.</exception>
    public static unsafe void GetBits<T>(this T value, Span<bool> bits)
        where T : unmanaged, IBinaryInteger<T>
    {
        var sizeInBits = sizeof(T) * 8;
        ArgumentOutOfRangeException.ThrowIfLessThan((uint)bits.Length, (uint)sizeInBits, nameof(bits));

        if (Vector.IsHardwareAccelerated && Vector<byte>.Count >= 8)
        {
            GetBitsVectorized(ref Unsafe.As<T, byte>(ref value),
                (uint)sizeof(T),
                ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetReference(bits)));
        }
        else
        {
            // software fallback
            for (var position = 0; position < sizeInBits; position++)
            {
                bits[position] = (value & (T.One << position)) != T.Zero;
            }
        }
    }

    private static void GetBitsVectorized(ref byte bytes, nuint length, ref byte bits)
    {
        const ulong mask = 0B_0000_0001UL << 0
                           | 0B_0000_0010UL << 8
                           | 0B_0000_0100UL << 16
                           | 0B_0000_1000UL << 24
                           | 0B_0001_0000UL << 32
                           | 0B_0010_0000UL << 40
                           | 0B_0100_0000UL << 48
                           | 0B_1000_0000UL << 56;
        
        var bitMask = Vector.AsVectorByte(Vector.CreateScalarUnsafe(mask));
        
        for (nuint i = 0; i < length; i++)
        {
            var inputByte = Vector.Create(Unsafe.Add(ref bytes, i));
            var v = Vector.AsVectorUInt64(Vector.Min(inputByte & bitMask, Vector<byte>.One));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref bits, i * 8), v.ToScalar());
        }
    }
}