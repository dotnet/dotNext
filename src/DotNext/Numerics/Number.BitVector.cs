using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace DotNext.Numerics;

public static partial class Number
{
    private const ulong BitMask = 0B_0000_0001UL << 0
                               | 0B_0000_0010UL << 8
                               | 0B_0000_0100UL << 16
                               | 0B_0000_1000UL << 24
                               | 0B_0001_0000UL << 32
                               | 0B_0010_0000UL << 40
                               | 0B_0100_0000UL << 48
                               | 0B_1000_0000UL << 56;
    
    /// <summary>
    /// Gets a value indicating that the specified bit is set.
    /// </summary>
    /// <param name="number">The number to inspect.</param>
    /// <param name="position">The position of the bit within <paramref name="number"/>.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    /// <returns><see langword="true"/> if the bit at <paramref name="position"/> is set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public static bool IsBitSet<T>(this T number, int position)
        where T : struct, IBinaryInteger<T>
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
        where T : struct, IBinaryInteger<T>
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
    public static unsafe TResult FromBits<TResult>(this ReadOnlySpan<bool> bits)
        where TResult : unmanaged, IBinaryInteger<TResult>
    {
        var sizeInBits = sizeof(TResult) * 8;
        var result = TResult.Zero;

        if (Bmi2.X64.IsSupported && sizeInBits <= bits.Length)
        {
            result = UsingBmi2(ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetReference(bits)));
        }
        else if (Vector.IsHardwareAccelerated && Vector<byte>.Count >= 8 && sizeInBits <= bits.Length)
        {
            result = Vectorized(ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetReference(bits)));
        }
        else
        {
            // software fallback
            for (var position = 0; position < bits.Length; position++)
            {
                if (bits[position])
                    result |= TResult.One << position;
            }
        }

        return result;
        
        static TResult Vectorized(ref byte bits)
        {
            Debug.Assert(Vector.IsHardwareAccelerated);
        
            var result = default(TResult);
            var bitMask = Vector.CreateScalar(BitMask);

            for (var i = 0; i < sizeof(TResult); i++)
            {
                var data = Vector.CreateScalar(Unsafe.ReadUnaligned<ulong>(in Unsafe.Add(ref bits, i * sizeof(ulong))));
                var vec = Vector.AsVectorByte(bitMask) * Vector.AsVectorByte(data);
                var octet = Vector.Sum(vec);
                result |= TResult.CreateTruncating(octet) << (i * sizeof(ulong));
            }

            return result;
        }
        
        static TResult UsingBmi2(ref byte bits)
        {
            Debug.Assert(Bmi2.X64.IsSupported);
        
            const ulong extractionMask = 0B_0000_0001UL << 0
                                         | 0B_0000_0001UL << 8
                                         | 0B_0000_0001UL << 16
                                         | 0B_0000_0001UL << 24
                                         | 0B_0000_0001UL << 32
                                         | 0B_0000_0001UL << 40
                                         | 0B_0000_0001UL << 48
                                         | 0B_0000_0001UL << 56;
        
            var result = default(TResult);

            for (var i = 0; i < sizeof(TResult); i++)
            {
                var data = Unsafe.ReadUnaligned<ulong>(in Unsafe.Add(ref bits, i * sizeof(ulong)));
                var octet = Bmi2.X64.ParallelBitExtract(data, extractionMask);
                result |= TResult.CreateTruncating(octet) << (i * sizeof(ulong));
            }

            return result;
        }
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
            Vectorized(ref Unsafe.As<T, byte>(ref value),
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
        
        static void Vectorized(ref byte bytes, nuint length, ref byte bits)
        {
            Debug.Assert(Vector.IsHardwareAccelerated);
        
            var bitMask = Vector.AsVectorByte(Vector.CreateScalarUnsafe(BitMask));
        
            for (nuint i = 0; i < length; i++)
            {
                var inputByte = Vector.Create(Unsafe.Add(ref bytes, i));
                var v = Vector.AsVectorUInt64(Vector.Min(inputByte & bitMask, Vector<byte>.One));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref bits, i * 8), v.ToScalar());
            }
        }
    }
}