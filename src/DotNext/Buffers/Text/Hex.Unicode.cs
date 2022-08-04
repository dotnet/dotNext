using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Buffers.Text;

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
            const int bytesCountPerIteration = sizeof(long);
            const int charsCountPerIteration = bytesCountPerIteration * 2;

            var nimbles = NimbleToUtf8CharLookupTable(lowercased);
            var lowNimblesMask = Vector128.Create(
                (short)NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue);

            var highNimblesMask = Vector128.Create(
                (short)(NimbleMaxValue << 4),
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4);

            var utf16Mask = Vector128.Create(
                (short)byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue);

            for (Vector128<short> input; offset >= Vector128<short>.Count; offset -= Vector128<short>.Count, bytePtr = ref Add(ref bytePtr, bytesCountPerIteration), charPtr = ref Add(ref charPtr, charsCountPerIteration))
            {
                input = Vector128.Create(
                    (short)bytePtr,
                    Add(ref bytePtr, 1),
                    Add(ref bytePtr, 2),
                    Add(ref bytePtr, 3),
                    Add(ref bytePtr, 4),
                    Add(ref bytePtr, 5),
                    Add(ref bytePtr, 6),
                    Add(ref bytePtr, 7));

                // apply x & 0B1111 for each vector component to get the lower nibbles;
                // then do table lookup
                var lowNimbles = Ssse3.And(input, lowNimblesMask).AsByte();
                lowNimbles = Ssse3.Shuffle(nimbles, lowNimbles);

                // apply (x & 0B1111_0000) >> 4 for each vector component to get the higher nibbles
                // then do table lookup
                var highNimbles = Sse3.ShiftRightLogical(Sse3.And(input, highNimblesMask).AsInt16(), 4).AsByte();
                highNimbles = Ssse3.Shuffle(nimbles, highNimbles);

                // combine high nimbles and low nimbles
                var result = Ssse3.UnpackLow(highNimbles.AsInt16(), lowNimbles.AsInt16());
                result = Ssse3.And(result, utf16Mask); // mask allows to remove garbage from high 8-bit of 16-bit char
                fixed (char* ptr = &charPtr)
                {
                    Ssse3.Store((short*)ptr, result);
                }

                result = Ssse3.UnpackHigh(highNimbles.AsInt16(), lowNimbles.AsInt16());
                result = Ssse3.And(result, utf16Mask);
                fixed (char* ptr = &charPtr)
                {
                    Ssse3.Store((short*)(ptr + bytesCountPerIteration), result);
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

        for (byte value; offset < bytesCount; offset++, charPtr = ref Add(ref charPtr, 1), bytePtr = ref Add(ref bytePtr, 1))
        {
            value = bytePtr;
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

        static string ToHexLowerCase(ReadOnlySpan<byte> bytes)
        {
            string result;

            var count = bytes.Length << 1;
            if (count is 0)
            {
                result = string.Empty;
            }
            else
            {
                result = new('\0', count);
                EncodeToUtf16(bytes, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference<char>(result), count), lowercased: true);
            }

            return result;
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