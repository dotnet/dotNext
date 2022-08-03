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
    /// <param name="output">The buffer used to write hexadecimal representation of bytes, in UTF-8 encoding.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
    public static unsafe int EncodeToUtf8(ReadOnlySpan<byte> bytes, Span<byte> output, bool lowercased = false)
    {
        if (bytes.IsEmpty || output.IsEmpty)
            return 0;

        int bytesCount = Math.Min(bytes.Length, output.Length >> 1), offset;
        ref byte bytePtr = ref MemoryMarshal.GetReference(bytes);
        ref byte charPtr = ref MemoryMarshal.GetReference(output);

        // use hardware intrinsics when possible
        if (Ssse3.IsSupported && bytesCount >= Vector128<short>.Count)
        {
            offset = bytesCount;

            // encode 8 bytes at a time using 128-bit vector (SSSE3 only intructions)
            const int bytesCountPerIteration = sizeof(long);
            const int charsCountPerIteration = bytesCountPerIteration * 2;

            var nimbles = NimbleToUtf8CharLookupTable(lowercased);

            var lowBitsMask = Vector128.Create(
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                NimbleMaxValue,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

            var highBitMask = Vector128.Create(
                (byte)(NimbleMaxValue << 4),
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                NimbleMaxValue << 4,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

            for (Vector128<byte> input; offset >= Vector128<short>.Count; offset -= Vector128<short>.Count, bytePtr = ref Add(ref bytePtr, bytesCountPerIteration), charPtr = ref Add(ref charPtr, charsCountPerIteration))
            {
                input = Vector128.CreateScalarUnsafe(ReadUnaligned<long>(ref bytePtr)).AsByte();

                // apply x & 0B1111 for each vector component to get the lower nibbles;
                // then do table lookup
                var lowNibbles = Ssse3.And(input, lowBitsMask).AsByte();
                lowNibbles = Ssse3.Shuffle(nimbles, lowNibbles);

                // apply (x & 0B1111_0000) >> 4 for each vector component to get the higher nibbles
                // then do table lookup
                var highNibbles = Sse3.ShiftRightLogical(Sse3.And(input, highBitMask).AsInt16(), 4).AsByte();
                highNibbles = Ssse3.Shuffle(nimbles, highNibbles);

                // encode to hex
                var result = Ssse3.UnpackLow(highNibbles, lowNibbles);

                fixed (byte* ptr = &charPtr)
                {
                    Ssse3.Store(ptr, result);
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
            charPtr = (byte)Add(ref hexTable, value >> 4);
            charPtr = ref Add(ref charPtr, 1);
            charPtr = (byte)Add(ref hexTable, value & NimbleMaxValue);
        }

        return bytesCount << 1;
    }

    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The hexadecimal representation of bytes.</returns>
    public static byte[] EncodeToUtf8(ReadOnlySpan<byte> bytes, bool lowercased = false)
    {
        byte[] result;

        var count = bytes.Length << 1;
        if (count is 0)
        {
            result = Array.Empty<byte>();
        }
        else
        {
            result = GC.AllocateUninitializedArray<byte>(count);
            count = EncodeToUtf8(bytes, result, lowercased);
        }

        return result;
    }

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The hexadecimal representation of bytes.</param>
    /// <param name="output">The output buffer used to write decoded bytes.</param>
    /// <returns>The actual number of bytes in <paramref name="output"/> written by the method.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    public static int DecodeFromUtf8(this ReadOnlySpan<byte> chars, Span<byte> output)
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