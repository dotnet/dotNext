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
        if (Ssse3.IsSupported)
        {
            offset = bytesCount;
            var nibbles = NibbleToUtf8CharLookupTable(lowercased);

            // encode 8 bytes at a time using 256-bit vector (AVX2 only intructions)
            if (Avx2.IsSupported && offset >= sizeof(ulong))
            {
                // 8 bytes to 16 hex chars to 32 unicode symbols
                const int bytesCountPerIteration = sizeof(ulong);
                const int charsCountPerIteration = bytesCountPerIteration * 2;

                var nibbles256 = Vector256.Create(nibbles, nibbles);
                var lowNibbleMask = Vector256.Create(NibbleMaxValue);
                var utf16Mask = Vector256.Create(
                    (byte)0,
                    byte.MaxValue,
                    1,
                    byte.MaxValue,
                    2,
                    byte.MaxValue,
                    3,
                    byte.MaxValue,
                    4,
                    byte.MaxValue,
                    5,
                    byte.MaxValue,
                    6,
                    byte.MaxValue,
                    7,
                    byte.MaxValue,
                    0,
                    byte.MaxValue,
                    1,
                    byte.MaxValue,
                    2,
                    byte.MaxValue,
                    3,
                    byte.MaxValue,
                    4,
                    byte.MaxValue,
                    5,
                    byte.MaxValue,
                    6,
                    byte.MaxValue,
                    7,
                    byte.MaxValue);

                do
                {
                    var lowNibbles = Vector256.Create(
                        Vector128.CreateScalarUnsafe(ReadUnaligned<uint>(ref bytePtr)),
                        Vector128.CreateScalarUnsafe(ReadUnaligned<uint>(ref Add(ref bytePtr, sizeof(uint)))))
                        .AsByte();
                    var highNibbles = Avx2.ShiftRightLogical(lowNibbles.AsUInt32(), 4).AsByte();

                    // combine high nibbles and low nibbles, then do table lookup
                    var result = Avx2.UnpackLow(highNibbles, lowNibbles);
                    result = Avx2.And(result, lowNibbleMask);
                    result = Avx2.Shuffle(nibbles256, result);
                    result = Avx2.Shuffle(result, utf16Mask);

                    fixed (char* ptr = &charPtr)
                    {
                        Avx2.Store((byte*)ptr, result);
                    }

                    bytePtr = ref Add(ref bytePtr, bytesCountPerIteration);
                    charPtr = ref Add(ref charPtr, charsCountPerIteration);
                    offset -= bytesCountPerIteration;
                }
                while (offset >= bytesCountPerIteration);
            }

            // encode 4 bytes at a time using 128-bit vector (SSSE3 only intructions)
            if (offset >= sizeof(uint))
            {
                // 4 bytes to 8 hex chars to 16 unicode symbols
                const int bytesCountPerIteration = sizeof(uint);
                const int charsCountPerIteration = bytesCountPerIteration * 2;

                var lowNibbleMask = Vector128.Create(NibbleMaxValue);
                var utf16Mask = Vector128.Create(
                    (byte)0,
                    byte.MaxValue,
                    1,
                    byte.MaxValue,
                    2,
                    byte.MaxValue,
                    3,
                    byte.MaxValue,
                    4,
                    byte.MaxValue,
                    5,
                    byte.MaxValue,
                    6,
                    byte.MaxValue,
                    7,
                    byte.MaxValue);

                do
                {
                    var lowNibbles = Vector128.CreateScalarUnsafe(ReadUnaligned<uint>(ref bytePtr)).AsByte();
                    var highNibbles = Sse2.ShiftRightLogical(lowNibbles.AsUInt32(), 4).AsByte();

                    // combine high nibbles and low nibbles, then do table lookup
                    var result = Sse2.UnpackLow(highNibbles, lowNibbles);
                    result = Sse2.And(result, lowNibbleMask);
                    result = Ssse3.Shuffle(nibbles, result);
                    result = Ssse3.Shuffle(result, utf16Mask);

                    fixed (char* ptr = &charPtr)
                    {
                        Sse2.Store((byte*)ptr, result);
                    }

                    bytePtr = ref Add(ref bytePtr, bytesCountPerIteration);
                    charPtr = ref Add(ref charPtr, charsCountPerIteration);
                    offset -= bytesCountPerIteration;
                }
                while (offset >= bytesCountPerIteration);
            }

            offset = bytesCount - offset;
        }
        else
        {
            offset = 0;
        }

        ref char hexTable = ref MemoryMarshal.GetArrayDataReference(NibbleToUtf16CharLookupTable);
        if (!lowercased)
            hexTable = ref Unsafe.Add(ref hexTable, 16);

        for (byte value; offset < bytesCount; offset++, charPtr = ref Add(ref charPtr, 1), bytePtr = ref Add(ref bytePtr, 1))
        {
            value = bytePtr;
            charPtr = Add(ref hexTable, value >> 4);
            charPtr = ref Add(ref charPtr, 1);
            charPtr = Add(ref hexTable, value & NibbleMaxValue);
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
        string result;

        var count = bytes.Length << 1;
        if (count is 0)
        {
            result = string.Empty;
        }
        else
        {
            result = new('\0', count);
            EncodeToUtf16(bytes, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference<char>(result), count), lowercased);
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

            bytePtr = (byte)(ToNibble(low) | (ToNibble(high) << 4));
        }

        return charCount >> 1;
    }
}