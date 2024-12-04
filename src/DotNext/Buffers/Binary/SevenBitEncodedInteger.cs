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
[StructLayout(LayoutKind.Auto)]
public struct SevenBitEncodedInteger<T>(T value) : ISupplier<T>, IResettable
    where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>
{
    /// <summary>
    /// Maximum size of encoded <typeparamref name="T"/>, in bytes.
    /// </summary>
    public static int MaxSizeInBytes { get; }
    
    private static readonly int MaxSizeInBits;
    private static readonly T Ox7FU;

    static SevenBitEncodedInteger()
    {
        var bitCount = Number.GetMaxByteCount<T>() * 8;
        bitCount = Math.DivRem(bitCount, 7, out var remainder);
        bitCount += Unsafe.BitCast<bool, byte>(remainder is not 0);
        
        MaxSizeInBytes = bitCount;
        MaxSizeInBits = MaxSizeInBytes * 7;
        Ox7FU = T.CreateTruncating(0x7FU);
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
            throw new InvalidDataException();

        value |= (T.CreateTruncating(b) & Ox7FU) << shift;
        shift += 7;
        return (b & 0x80U) is not 0U;
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
        private static readonly T OnesComplement0x7FU = ~Ox7FU;
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

            if (value > Ox7FU)
            {
                current = byte.CreateTruncating(value | OnesComplement0x7FU);
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