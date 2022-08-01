using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace DotNext.Buffers.Text;

/// <summary>
/// Provides conversion to/from hexadecimal representation.
/// </summary>
public static partial class Hex
{
    private const byte NimbleMaxValue = 0B1111;

    private static readonly char[] NimbleToUtf16CharLookupTable = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> NimbleToUtf8CharLookupTable(bool lowercased) => lowercased
                    ? Vector128.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f')
                    : Vector128.Create((byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F');

    // byte insertion mask allows to prepare input vector of bytes for logical right shift operator
    // to do this, we need to convert (shuffle) vector of bytes to vector of shorts a follows:
    // 1, 2, 3, 4, 5, 6, 7, 8 => 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0, 8, 0
    private static Vector128<byte> SaturationMask => Vector128.Create(
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

    private static Vector128<short> HighBitsMask => Vector128.Create(
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4,
        NimbleMaxValue << 4);

    private static byte ToNimble(int ch)
    {
        var table = CharToNimbleLookupTable;

        byte result;
        if ((uint)ch >= (uint)table.Length || ((result = table[ch]) > NimbleMaxValue))
            ThrowFormatException(ch);

        return result;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowFormatException(int ch) => throw new FormatException(ExceptionMessages.InvalidHexInput((char)ch));
    }
}