using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

using Numerics;

/// <summary>
/// Represents encoder and decoder for 7-bit encoded integers.
/// </summary>
/// <param name="value">The value to decode.</param>
/// <typeparam name="T">The type of the integer.</typeparam>
/// <seealso href="https://en.wikipedia.org/wiki/LEB128">LEB128 encoding</seealso>
[StructLayout(LayoutKind.Auto)]
public struct ULeb128<T>(T value) : ISupplier<T>, IResettable
    where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>
{
    /// <summary>
    /// Maximum size of encoded <typeparamref name="T"/>, in bytes.
    /// </summary>
    public static int MaxSizeInBytes { get; }
    
    private static readonly int MaxSizeInBits;
    private const byte BitMask = 0x7F;

    static ULeb128()
    {
        var bitCount = Number.GetMaxByteCount<T>() * 8;
        bitCount = Math.DivRem(bitCount, 7, out var remainder);
        bitCount += Unsafe.BitCast<bool, byte>(remainder is not 0);
        
        MaxSizeInBytes = bitCount;
        MaxSizeInBits = bitCount * 7;
    }

    private int shift;

    /// <summary>
    /// Decodes an octet.
    /// </summary>
    /// <param name="b">The byte that represents a part of 7-bit encoded integer.</param>
    /// <returns><see langword="true"/> if the decoder expects more data to decode; <see langword="false"/> if the last octet detected.</returns>
    /// <exception cref="InvalidDataException">The maximum number of octets reached.</exception>
    public bool Append(byte b)
    {
        if (shift == MaxSizeInBits)
            ThrowInvalidDataException();

        value |= (T.CreateTruncating(b) & T.CreateTruncating(BitMask)) << shift;
        shift += 7;
        return (b & 0x80U) is not 0U;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidDataException()
            => throw new InvalidDataException();
    }

    /// <summary>
    /// Resets the decoder.
    /// </summary>
    public void Reset()
    {
        shift = 0;
        value = default;
    }

    /// <summary>
    /// Gets a value represented by the encoded.
    /// </summary>
    public readonly T Value => value;
    
    /// <inheritdoc/>
    readonly T ISupplier<T>.Invoke() => value;

    /// <summary>
    /// Gets an enumerator over encoded octets.
    /// </summary>
    /// <returns></returns>
    public readonly Enumerator GetEnumerator() => new(value);

    /// <summary>
    /// Represents an enumerator that produces 7-bit encoded integer as a sequence of octets.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private T value;
        private byte current;
        private bool completed;

        internal Enumerator(T value) => this.value = value;

        /// <summary>
        /// The current octet.
        /// </summary>
        public readonly byte Current => current;

        /// <summary>
        /// Moves to the next octet.
        /// </summary>
        /// <returns><see langword="true"/> if one more octet is produced; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (completed)
                return false;

            var allBitsSet = T.CreateTruncating(BitMask);
            if (value > allBitsSet)
            {
                current = byte.CreateTruncating(value | ~allBitsSet);
                value >>>= 7;
            }
            else
            {
                current = byte.CreateTruncating(value);
                completed = true;
            }

            return true;
        }
    }
}