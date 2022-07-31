using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
    public static unsafe int ToHex(this ReadOnlySpan<byte> bytes, Span<char> output, bool lowercased = false)
    {
        if (bytes.IsEmpty || output.IsEmpty)
            return 0;

        int bytesCount = Math.Min(bytes.Length, output.Length >> 1), offset;
        ref byte bytePtr = ref MemoryMarshal.GetReference(bytes);
        ref char charPtr = ref MemoryMarshal.GetReference(output);

        // use hardware intrinsics when possible
        if (Ssse3.IsSupported)
        {
            const short highBits = NimbleMaxValue << 4;
            offset = bytesCount;

            // encode 16 bytes at a time using 256-bit vector
            if (Avx2.IsSupported && offset >= Vector256<short>.Count)
            {
                const int bytesCountPerIteration = 16;
                const int charsCountPerIteration = bytesCountPerIteration * 2;
                var nibblesMask = Vector256.Create(byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0);

                // byte insertion mask allows to prepare input vector of bytes for logical right shift operator
                // to do this, we need to convert vector of bytes to vector of shorts
                var insertionMask = Vector256.Create(
                0,
                byte.MaxValue,
                1,
                byte.MaxValue,
                2,
                byte.MaxValue,
                3,
                byte.MaxValue,
                8,
                byte.MaxValue,
                9,
                byte.MaxValue,
                10,
                byte.MaxValue,
                11,
                byte.MaxValue,
                4,
                byte.MaxValue,
                5,
                byte.MaxValue,
                6,
                byte.MaxValue,
                7,
                byte.MaxValue,
                12,
                byte.MaxValue,
                13,
                byte.MaxValue,
                14,
                byte.MaxValue,
                15,
                byte.MaxValue
                );

                var lowBitsMask = Vector256.Create(NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue);
                var highBitsMask = Vector256.Create(highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits);
                var asciiTable = lowercased
                    ? Vector256.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f', (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f')
                    : Vector256.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F');

                for (Vector256<short> input; offset >= Vector256<short>.Count; offset -= Vector256<short>.Count, bytePtr = ref Add(ref bytePtr, bytesCountPerIteration), charPtr = ref Add(ref charPtr, charsCountPerIteration))
                {
                    var lowQword = ReadUnaligned<ulong>(ref bytePtr);
                    var hiQword = ReadUnaligned<ulong>(ref Add(ref bytePtr, sizeof(ulong)));
                    input = Avx2.Shuffle(Vector256.Create(lowQword, hiQword, lowQword, hiQword).AsByte(), insertionMask).AsInt16();

                    // apply x & 0B1111 for each vector component to get the lower nibbles;
                    // then do table lookup
                    var lowNibbles = Avx2.Shuffle(asciiTable, Avx2.And(input, lowBitsMask).AsByte());

                    // reset to zero all unused components
                    lowNibbles = Avx2.And(lowNibbles, nibblesMask);

                    // apply (x & 0B1111_0000) >> 4 for each vector component to get the higher nibbles
                    // then do table lookup
                    var highNibbles = Avx2.Shuffle(asciiTable, Avx2.ShiftRightLogical(Avx2.And(input, highBitsMask), 4).AsByte());

                    // reset to zero all unused components
                    highNibbles = Avx2.And(highNibbles, nibblesMask);

                    // encode to hex
                    var portion1 = Avx2.UnpackLow(highNibbles.AsInt16(), lowNibbles.AsInt16());
                    var portion2 = Avx2.UnpackHigh(highNibbles.AsInt16(), lowNibbles.AsInt16());

                    fixed (char* ptr = &charPtr)
                    {
                        Avx2.Store((short*)ptr, portion1);
                        Avx2.Store((short*)(ptr + bytesCountPerIteration), portion2);
                    }
                }
            }

            // encode 8 bytes at a time using 128-bit vector (SSSE3 only intructions)
            if (offset >= Vector128<short>.Count)
            {
                const int bytesCountPerIteration = sizeof(long);
                const int charsCountPerIteration = bytesCountPerIteration * 2;

                var nibblesMask = Vector128.Create(byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0, byte.MaxValue, 0);

                // byte insertion mask allows to prepare input vector of bytes for logical right shift operator
                // to do this, we need to convert vector of bytes to vector of shorts a follows:
                // 1, 2, 3, 4, 5, 6, 7, 8 => 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0, 8, 0
                var insertionMask = Vector128.Create(0, byte.MaxValue, 1, byte.MaxValue, 2, byte.MaxValue, 3, byte.MaxValue, 4, byte.MaxValue, 5, byte.MaxValue, 6, byte.MaxValue, 7, byte.MaxValue);
                var lowBitsMask = Vector128.Create(NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue, NimbleMaxValue);
                var highBitsMask = Vector128.Create(highBits, highBits, highBits, highBits, highBits, highBits, highBits, highBits);
                var asciiTable = lowercased
                    ? Vector128.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f')
                    : Vector128.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F');

                for (Vector128<short> input; offset >= Vector128<short>.Count; offset -= Vector128<short>.Count, bytePtr = ref Add(ref bytePtr, bytesCountPerIteration), charPtr = ref Add(ref charPtr, charsCountPerIteration))
                {
                    input = Ssse3.Shuffle(Vector128.CreateScalarUnsafe(ReadUnaligned<long>(ref bytePtr)).AsByte(), insertionMask).AsInt16();

                    // apply x & 0B1111 for each vector component to get the lower nibbles;
                    // then do table lookup
                    var lowNibbles = Ssse3.Shuffle(asciiTable, Ssse3.And(input, lowBitsMask).AsByte());

                    // reset to zero all unused components
                    lowNibbles = Ssse3.And(lowNibbles, nibblesMask);

                    // apply (x & 0B1111_0000) >> 4 for each vector component to get the higher nibbles
                    // then do table lookup
                    var highNibbles = Ssse3.Shuffle(asciiTable, Ssse3.ShiftRightLogical(Ssse3.And(input, highBitsMask), 4).AsByte());

                    // reset to zero all unused components
                    highNibbles = Ssse3.And(highNibbles, nibblesMask);

                    // encode to hex
                    var portion1 = Ssse3.UnpackLow(highNibbles.AsInt16(), lowNibbles.AsInt16());
                    var portion2 = Ssse3.UnpackHigh(highNibbles.AsInt16(), lowNibbles.AsInt16());

                    fixed (char* ptr = &charPtr)
                    {
                        Ssse3.Store((short*)ptr, portion1);
                        Ssse3.Store((short*)(ptr + bytesCountPerIteration), portion2);
                    }
                }
            }

            offset = bytesCount - offset;
        }
        else
        {
            offset = 0;
        }

        ref char hexTable = ref MemoryMarshal.GetArrayDataReference(NimbleToCharLookupTable);
        if (!lowercased)
            hexTable = ref Unsafe.Add(ref hexTable, 16);

        for (; offset < bytesCount; offset++, charPtr = ref Add(ref charPtr, 1), bytePtr = ref Add(ref bytePtr, 1))
        {
            var value = bytePtr;
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
    public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercased = false)
    {
        return lowercased ? ToHexLowerCase(bytes) : Convert.ToHexString(bytes);

        [SkipLocalsInit]
        static string ToHexLowerCase(ReadOnlySpan<byte> bytes)
        {
            var count = bytes.Length << 1;
            if (count is 0)
                return string.Empty;

            using MemoryRental<char> buffer = (uint)count <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[count] : new MemoryRental<char>(count);
            count = ToHex(bytes, buffer.Span, lowercased: true);
            return new string(buffer.Span.Slice(0, count));
        }
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

        return charCount >> 1;

        static byte ToNimble(int ch)
        {
            var table = CharToNimbleLookupTable;

            byte result;
            if ((uint)ch >= (uint)table.Length || ((result = table[ch]) > NimbleMaxValue))
                ThrowFormatException(ch);

            return result;
        }

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowFormatException(int ch) => throw new FormatException(ExceptionMessages.InvalidHexInput((char)ch));
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