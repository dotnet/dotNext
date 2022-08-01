using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Buffers.Text;

using Buffers;

public static partial class Hex
{
    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="output">The buffer used to write hexadecimal representation of bytes.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
    public static unsafe int EncodeToUtf16(ReadOnlySpan<byte> bytes, Span<char> output, bool lowercased = false)
    {
        if (bytes.IsEmpty || output.IsEmpty)
            return 0;

        int bytesCount = Math.Min(bytes.Length, output.Length >> 1), offset;
        ref byte bytePtr = ref MemoryMarshal.GetReference(bytes);
        ref char charPtr = ref MemoryMarshal.GetReference(output);

        // use hardware intrinsics when possible
        if (Ssse3.IsSupported && bytesCount >= Vector128<short>.Count)
        {
            offset = bytesCount;

            // encode 8 bytes at a time using 128-bit vector (SSSE3 only intructions)
            if (offset >= Vector128<short>.Count)
            {
                const int bytesCountPerIteration = sizeof(long);
                const int charsCountPerIteration = bytesCountPerIteration * 2;

                var nibblesMask = Vector128.Create(
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    0);

                var lowBitsMask = Vector128.Create(
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue,
                    NimbleMaxValue);

                var nimbles = NimbleToUtf8CharLookupTable(lowercased);

                for (Vector128<short> input; offset >= Vector128<short>.Count; offset -= Vector128<short>.Count, bytePtr = ref Add(ref bytePtr, bytesCountPerIteration), charPtr = ref Add(ref charPtr, charsCountPerIteration))
                {
                    input = Ssse3.Shuffle(Vector128.CreateScalarUnsafe(ReadUnaligned<long>(ref bytePtr)).AsByte(), SaturationMask).AsInt16();

                    // apply x & 0B1111 for each vector component to get the lower nibbles;
                    // then do table lookup
                    var lowNibbles = Ssse3.Shuffle(nimbles, Ssse3.And(input, lowBitsMask).AsByte());

                    // reset to zero all unused components
                    lowNibbles = Ssse3.And(lowNibbles, nibblesMask);

                    // apply (x & 0B1111_0000) >> 4 for each vector component to get the higher nibbles
                    // then do table lookup
                    var highNibbles = Ssse3.Shuffle(nimbles, Ssse3.ShiftRightLogical(Ssse3.And(input, HighBitsMask), 4).AsByte());

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

        ref char hexTable = ref MemoryMarshal.GetArrayDataReference(NimbleToUtf16CharLookupTable);
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
    public static string EncodeToUtf16(ReadOnlySpan<byte> bytes, bool lowercased = false)
    {
        return lowercased ? ToHexLowerCase(bytes) : Convert.ToHexString(bytes);

        [SkipLocalsInit]
        static string ToHexLowerCase(ReadOnlySpan<byte> bytes)
        {
            var count = bytes.Length << 1;
            if (count is 0)
                return string.Empty;

            using MemoryRental<char> buffer = (uint)count <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[count] : new MemoryRental<char>(count);
            count = EncodeToUtf16(bytes, buffer.Span, lowercased: true);
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
    public static int DecodeFromUtf16(ReadOnlySpan<char> chars, Span<byte> output)
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
    }
}