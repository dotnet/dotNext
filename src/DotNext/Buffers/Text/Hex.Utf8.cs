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
        if (Ssse3.IsSupported)
        {
            offset = bytesCount;

            var nibbles = NibbleToUtf8CharLookupTable(lowercased);

            // encode 16 bytes at a time using 256-bit vector (AVX2 only intructions)
            if (Avx2.IsSupported && offset >= 16)
            {
                const int bytesCountPerIteration = sizeof(ulong) * 2;
                const int charsCountPerIteration = bytesCountPerIteration * 2;
                var nibbles256 = Vector256.Create(nibbles, nibbles);
                var lowNibbleMask = Vector256.Create(NibbleMaxValue);

                do
                {
                    var lowNibbles = Vector256.Create(
                        Vector128.CreateScalarUnsafe(ReadUnaligned<ulong>(ref bytePtr)),
                        Vector128.CreateScalarUnsafe(ReadUnaligned<ulong>(ref Add(ref bytePtr, sizeof(ulong)))))
                        .AsByte();
                    var highNibbles = Avx2.ShiftRightLogical(lowNibbles.AsUInt32(), 4).AsByte();

                    // combine high nibbles and low nibbles, then do table lookup
                    var result = Avx2.UnpackLow(highNibbles, lowNibbles);
                    result = Avx2.And(result, lowNibbleMask);
                    result = Avx2.Shuffle(nibbles256, result);

                    fixed (byte* ptr = &charPtr)
                    {
                        Avx2.Store(ptr, result);
                    }

                    bytePtr = ref Add(ref bytePtr, bytesCountPerIteration);
                    charPtr = ref Add(ref charPtr, charsCountPerIteration);
                    offset -= bytesCountPerIteration;
                }
                while (offset >= bytesCountPerIteration);
            }

            // encode 8 bytes at a time using 128-bit vector (SSSE3 only intructions)
            if (offset >= sizeof(ulong))
            {
                const int bytesCountPerIteration = sizeof(ulong);
                const int charsCountPerIteration = bytesCountPerIteration * 2;
                var lowNibbleMask = Vector128.Create(NibbleMaxValue);

                do
                {
                    var lowNibbles = Vector128.CreateScalarUnsafe(ReadUnaligned<ulong>(ref bytePtr)).AsByte();
                    var highNibbles = Sse2.ShiftRightLogical(lowNibbles.AsUInt64(), 4).AsByte();

                    // combine high nibbles and low nibbles, then do table lookup
                    var result = Sse2.UnpackLow(highNibbles, lowNibbles);
                    result = Sse2.And(result, lowNibbleMask);
                    result = Ssse3.Shuffle(nibbles, result);

                    fixed (byte* ptr = &charPtr)
                    {
                        Sse2.Store(ptr, result);
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

        for (; offset < bytesCount; offset++, charPtr = ref Add(ref charPtr, 1), bytePtr = ref Add(ref bytePtr, 1))
        {
            var value = bytePtr;
            charPtr = (byte)Add(ref hexTable, value >> 4);
            charPtr = ref Add(ref charPtr, 1);
            charPtr = (byte)Add(ref hexTable, value & NibbleMaxValue);
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

            bytePtr = (byte)(ToNibble(low) | (ToNibble(high) << 4));
        }

        return charCount >> 1;
    }
}