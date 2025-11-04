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
/// <remarks>
/// Note that encoding of signed and unsigned integers produce different octets.
/// </remarks>
/// <typeparam name="T">The type of the integer.</typeparam>
/// <seealso href="https://en.wikipedia.org/wiki/LEB128">LEB128 encoding</seealso>
[StructLayout(LayoutKind.Auto)]
public struct Leb128<T> : ISupplier<T>, IResettable
    where T : struct, IBinaryInteger<T>
{
    /// <summary>
    /// Maximum size of encoded <typeparamref name="T"/>, in bytes.
    /// </summary>
    public static int MaxSizeInBytes { get; }
    
    private static readonly int MaxSizeInBits;
    private const byte BitMask = 0x7F;
    private const byte CarryBit = BitMask + 1;

    static Leb128()
    {
        var bitCount = Number.GetMaxByteCount<T>() * 8;
        bitCount = Math.DivRem(bitCount, 7, out var remainder);
        bitCount += Unsafe.BitCast<bool, byte>(remainder is not 0);
        
        MaxSizeInBytes = bitCount;
        MaxSizeInBits = bitCount * 7;
    }

    private ushort shift;
    private T value;

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

        var nextOctetExpected = Unsafe.BitCast<byte, bool>((byte)(b >> 7));
        const byte signBit = 0x40;

        // return back sign bit for signed integers
        if (Number.IsSigned<T>() && !nextOctetExpected && shift < MaxSizeInBits && (b & signBit) is not 0)
            value |= T.AllBitsSet << shift;

        return nextOctetExpected;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidDataException()
            => throw new InvalidDataException();
    }

    /// <summary>
    /// Resets the decoder.
    /// </summary>
    public void Reset() => Value = default;

    /// <summary>
    /// Gets a value represented by the encoder.
    /// </summary>
    public T Value
    {
        readonly get => value;
        set
        {
            shift = 0;
            this.value = value;
        }
    }
    
    /// <inheritdoc/>
    readonly T ISupplier<T>.Invoke() => value;

    /// <summary>
    /// Gets an enumerator over encoded octets.
    /// </summary>
    /// <returns></returns>
    public readonly Enumerator GetEnumerator() => new(value);

    /// <summary>
    /// Tries to encode the value by using LEB128 binary format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if <paramref name="buffer"/> has enough space to save the encoded value; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetBytes(T value, Span<byte> buffer, out int bytesWritten)
    {
        bytesWritten = 0;
        var index = 0;
        foreach (var octet in new Leb128<T> { Value = value })
        {
            if ((uint)index >= (uint)buffer.Length)
                return false;

            buffer[index++] = octet;
        }

        bytesWritten = index;
        return true;
    }

    /// <summary>
    /// Decodes LEB128-encoded integer.
    /// </summary>
    /// <param name="buffer">The input buffer containing LEB128 octets.</param>
    /// <param name="result">The decoded value.</param>
    /// <param name="bytesConsumed">The number of bytes consumed from <paramref name="buffer"/>.</param>
    /// <returns><see langword="true"/> if operation is successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> buffer, out T result, out int bytesConsumed)
    {
        bytesConsumed = 0;
        var decoder = new Leb128<T>();
        var successful = false;

        foreach (var octet in buffer)
        {
            bytesConsumed += 1;
            if (successful = !decoder.Append(octet))
                break;
        }

        result = decoder.Value;
        return successful;
    }

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

            if (Number.IsSigned<T>())
            {
                MoveNextSigned();
            }
            else
            {
                MoveNextUnsigned();
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveNextSigned()
        {
            var sevenBits = value & T.CreateTruncating(BitMask);
            value >>= 7;

            var octet = byte.CreateTruncating(sevenBits);
            if (value == -T.CreateTruncating((octet >>> 6) & 1))
            {
                completed = true;
            }
            else
            {
                octet = (byte)(octet | CarryBit);
            }

            current = octet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveNextUnsigned()
        {
            if (value > T.CreateTruncating(BitMask))
            {
                current = (byte)(byte.CreateTruncating(value) | CarryBit);
                value >>>= 7;
            }
            else
            {
                current = byte.CreateTruncating(value);
                completed = true;
            }
        }
    }
}