using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Numerics;

/// <summary>
/// Allows to convert bit vectors to scalar values.
/// </summary>
public static class BitVector
{
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

        if (Vector256.IsHardwareAccelerated && (sizeInBits & 1) is 0)
        {
            Get16Bits(ref Unsafe.As<T, byte>(ref value), sizeof(T), ref MemoryMarshal.GetReference(bits));
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Get8Bits(ref Unsafe.As<T, byte>(ref value), sizeof(T), ref MemoryMarshal.GetReference(bits));
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

    private static void Get8Bits(ref byte input, nint length, ref bool output)
    {
        Debug.Assert(Vector128.IsHardwareAccelerated);
        Debug.Assert(Vector64.IsHardwareAccelerated);

        for (nint i = 0; i < length; i += sizeof(byte))
        {
            Get8Bits(
                Vector128.Create(Unsafe.Add(ref input, i)),
                ref Unsafe.Add(ref output, i * 8));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Get8Bits(Vector128<byte> input, ref bool output)
    {
        Debug.Assert(Vector128.IsHardwareAccelerated);
        Debug.Assert(Vector64.IsHardwareAccelerated);

        var onevec = Vector128.Create((byte)1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var bitmask = Vector128.Create(
            0B_0000_0001,
            0B_0000_0010,
            0B_0000_0100,
            0B_0000_1000,
            0B_0001_0000,
            0B_0010_0000,
            0B_0100_0000,
            0B_1000_0000,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        Vector128.Min(input & bitmask, onevec)
            .GetLower().StoreUnsafe(ref Unsafe.As<bool, byte>(ref output));
    }

    private static void Get16Bits(ref byte input, nint length, ref bool output)
    {
        Debug.Assert(Vector128.IsHardwareAccelerated);

        for (nint i = 0; i < length; i += sizeof(ushort))
        {
            Get16Bits(
                Vector256.Create(Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref input, i))),
                ref Unsafe.Add(ref output, i * 8));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Get16Bits(Vector256<ushort> input, ref bool output)
    {
        Debug.Assert(Vector256.IsHardwareAccelerated);

        var onevec = Vector256.Create((ushort)1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var bitmask = Vector256.Create(
            0B_0000_0000_0000_0001,
            0B_0000_0000_0000_0010,
            0B_0000_0000_0000_0100,
            0B_0000_0000_0000_1000,
            0B_0000_0000_0001_0000,
            0B_0000_0000_0010_0000,
            0B_0000_0000_0100_0000,
            0B_0000_0000_1000_0000,
            0B_0000_0001_0000_0000,
            0B_0000_0010_0000_0000,
            0B_0000_0100_0000_0000,
            0B_0000_1000_0000_0000,
            0B_0001_0000_0000_0000,
            0B_0010_0000_0000_0000,
            0B_0100_0000_0000_0000,
            0B_1000_0000_0000_0000);

        // normalize first 8 bits for each 128-bit subvector
        var shuffleMask = Vector256.Create(
            0,
            2,
            4,
            6,
            8,
            10,
            12,
            14,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            16,
            18,
            20,
            22,
            24,
            26,
            28,
            30,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue);

        var result = Vector256.Shuffle(
            Vector256.Min(input & bitmask, onevec).AsByte(),
            shuffleMask);

        result.GetLower().GetLower().StoreUnsafe(ref Unsafe.As<bool, byte>(ref output));
        result.GetUpper().GetLower().StoreUnsafe(ref Unsafe.Add(ref Unsafe.As<bool, byte>(ref output), 8));
    }
}