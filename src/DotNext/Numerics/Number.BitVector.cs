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
    /// Extends <see cref="IBinaryInteger{TSelf}"/> interface.
    /// </summary>
    /// <param name="number">The number to inspect.</param>
    /// <typeparam name="T">The type of the number.</typeparam>
    extension<T>(T number) where T : unmanaged, IBinaryInteger<T>
    {
        /// <summary>
        /// Gets a value indicating that the specified bit is set.
        /// </summary>
        /// <param name="position">The position of the bit within <paramref name="number"/>.</param>
        /// <returns><see langword="true"/> if the bit at <paramref name="position"/> is set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
        public bool IsBitSet(int position)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(position);

            return (number & (T.One << position)) != T.Zero;
        }

        /// <summary>
        /// Sets the bit at the specified position.
        /// </summary>
        /// <param name="position">The position of the bit to set.</param>
        /// <param name="value">The bit value.</param>
        /// <returns>A modified number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
        public T SetBit(int position, bool value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(position);

            var bit = T.One << position;
            return value
                ? number | bit
                : number & ~bit;
        }

        /// <summary>
        /// Converts bit vector to a value of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="bits">A vector of bits.</param>
        /// <returns>A value of type <typeparamref name="T"/> restored from the vector of bits.</returns>
        public static unsafe T FromBits(ReadOnlySpan<bool> bits)
        {
            var sizeInBits = sizeof(T) * 8;
            var result = T.Zero;

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
                        result |= T.One << position;
                }
            }

            return result;

            static T Vectorized(ref byte bits)
            {
                Debug.Assert(Vector.IsHardwareAccelerated);

                var result = default(T);
                var bitMask = Vector.CreateScalar(BitMask);

                for (var i = 0; i < sizeof(T); i++)
                {
                    var data = Vector.CreateScalar(Unsafe.ReadUnaligned<ulong>(in Unsafe.Add(ref bits, i * sizeof(ulong))));
                    var vec = Vector.AsVectorByte(bitMask) * Vector.AsVectorByte(data);
                    var octet = Vector.Sum(vec);
                    result |= T.CreateTruncating(octet) << (i * sizeof(ulong));
                }

                return result;
            }

            static T UsingBmi2(ref byte bits)
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

                var result = default(T);

                for (var i = 0; i < sizeof(T); i++)
                {
                    var data = Unsafe.ReadUnaligned<ulong>(in Unsafe.Add(ref bits, i * sizeof(ulong)));
                    var octet = Bmi2.X64.ParallelBitExtract(data, extractionMask);
                    result |= T.CreateTruncating(octet) << (i * sizeof(ulong));
                }

                return result;
            }
        }

        /// <summary>
        /// Converts a value to a set of bits.
        /// </summary>
        /// <param name="bits">A buffer to be modified.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bits"/> has not enough length.</exception>
        public unsafe void GetBits(Span<bool> bits)
        {
            var sizeInBits = sizeof(T) * 8;
            ArgumentOutOfRangeException.ThrowIfLessThan((uint)bits.Length, (uint)sizeInBits, nameof(bits));

            if (Vector.IsHardwareAccelerated && Vector<byte>.Count >= 8)
            {
                Vectorized(ref Unsafe.As<T, byte>(ref number),
                    (uint)sizeof(T),
                    ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetReference(bits)));
            }
            else
            {
                // software fallback
                for (var position = 0; position < sizeInBits; position++)
                {
                    bits[position] = (number & (T.One << position)) != T.Zero;
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
}