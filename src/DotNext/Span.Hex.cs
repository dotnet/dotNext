using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using Buffers;

public static partial class Span
{
    private const byte NimbleMaxValue = 0B1111;

    private static readonly char[] NimbleToCharLookupTable = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    private static ReadOnlySpan<byte> CharToNimbleLookupTable => new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
            0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
            0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
            0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf, // 102
        };

    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="output">The buffer used to write hexadecimal representation of bytes.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
    public static int ToHex(this ReadOnlySpan<byte> bytes, Span<char> output, bool lowercased = false)
    {
        if (bytes.IsEmpty || output.IsEmpty)
            return 0;
        var bytesCount = Math.Min(bytes.Length, output.Length >> 1);
        ref byte firstByte = ref MemoryMarshal.GetReference(bytes);
        ref char charPtr = ref MemoryMarshal.GetReference(output);
        ref char hexTable = ref MemoryMarshal.GetArrayDataReference(NimbleToCharLookupTable);
        if (!lowercased)
            hexTable = ref Unsafe.Add(ref hexTable, 16);
        for (var i = 0; i < bytesCount; i++, charPtr = ref Add(ref charPtr, 1))
        {
            var value = Add(ref firstByte, i);
            charPtr = Add(ref hexTable, value >> 4);
            charPtr = ref Add(ref charPtr, 1);
            charPtr = Add(ref hexTable, value & NimbleMaxValue);
        }

        return bytesCount << 1;
    }

    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The hexadecimal representation of bytes.</returns>
    [SkipLocalsInit]
    public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercased = false)
    {
        if (lowercased)
        {
            var count = bytes.Length << 1;
            if (count is 0)
                return string.Empty;

            using MemoryRental<char> buffer = (uint)count <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[count] : new MemoryRental<char>(count);
            count = ToHex(bytes, buffer.Span, lowercased);
            return new string(buffer.Span.Slice(0, count));
        }

        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The hexadecimal representation of bytes.</param>
    /// <param name="output">The output buffer used to write decoded bytes.</param>
    /// <returns>The actual number of bytes in <paramref name="output"/> written by the method.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    public static int FromHex(this ReadOnlySpan<char> chars, Span<byte> output)
    {
        if (chars.IsEmpty || output.IsEmpty)
            return 0;
        var charCount = Math.Min(chars.Length, output.Length << 1);
        charCount &= -2; // convert even to odd integer
        ref byte bytePtr = ref MemoryMarshal.GetReference(output);
        for (var i = 0; i < charCount; i += 2, bytePtr = ref Add(ref bytePtr, 1))
        {
            var high = Unsafe.Add(ref MemoryMarshal.GetReference(chars), i);
            var low = Unsafe.Add(ref MemoryMarshal.GetReference(chars), i + 1);

            bytePtr = (byte)(ToNimble(low) | (ToNimble(high) << 4));
        }

        return charCount / 2;

        static byte ToNimble(int ch)
        {
            var table = CharToNimbleLookupTable;

            byte result;
            return (uint)ch < (uint)table.Length && ((result = table[ch]) <= NimbleMaxValue)
                ? result
                : throw new FormatException(ExceptionMessages.InvalidHexInput((char)ch));
        }
    }

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The characters containing hexadecimal representation of bytes.</param>
    /// <returns>The decoded array of bytes.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    [Obsolete("Use Convert.FromHexString() method instead")]
    public static byte[] FromHex(this ReadOnlySpan<char> chars) => Convert.FromHexString(chars);
}