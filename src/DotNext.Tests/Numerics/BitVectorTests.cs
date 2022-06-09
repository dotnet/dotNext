using System.Diagnostics.CodeAnalysis;

namespace DotNext.Numerics;

[ExcludeFromCodeCoverage]
public sealed class BitVectorTests : Test
{
    private static ReadOnlySpan<bool> CreateVector(int size, bool value)
    {
        var result = new bool[size];
        Array.Fill(result, value);
        return result;
    }

    [Fact]
    public static void BitsToByte()
    {
        Equal(0, BitVector.ToByte(ReadOnlySpan<bool>.Empty));
        Equal(3, BitVector.ToByte(stackalloc bool[] { true, true }));
        Equal(8, BitVector.ToByte(stackalloc bool[] { false, false, false, true }));
        Equal(byte.MaxValue, BitVector.ToByte(CreateVector(8, true)));
    }

    [Fact]
    public static void ByteToBits()
    {
        var value = byte.MaxValue;
        var buffer = new bool[8];

        BitVector.FromByte(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromByte(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromByte(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToSByte()
    {
        Equal(0, BitVector.ToSByte(ReadOnlySpan<bool>.Empty));
        Equal(3, BitVector.ToSByte(stackalloc bool[] { true, true }));
        Equal(8, BitVector.ToSByte(stackalloc bool[] { false, false, false, true }));
        Equal(-1, BitVector.ToSByte(CreateVector(8, true)));
    }

    [Fact]
    public static void SByteToBits()
    {
        sbyte value = -1;
        var buffer = new bool[8];

        BitVector.FromSByte(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromSByte(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromSByte(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt16()
    {
        Equal(0, BitVector.ToInt16(ReadOnlySpan<bool>.Empty));
        Equal(3, BitVector.ToInt16(stackalloc bool[] { true, true }));
        Equal(8, BitVector.ToInt16(stackalloc bool[] { false, false, false, true }));
        Equal(short.MaxValue, BitVector.ToInt16(CreateVector(15, true)));
        Equal(-1, BitVector.ToInt16(CreateVector(16, true)));
    }

    [Fact]
    public static void Int16ToBits()
    {
        short value = -1;
        var buffer = new bool[16];

        BitVector.FromInt16(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromInt16(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromInt16(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToUInt16()
    {
        Equal(0, BitVector.ToUInt16(ReadOnlySpan<bool>.Empty));
        Equal(3, BitVector.ToUInt16(stackalloc bool[] { true, true }));
        Equal(8, BitVector.ToUInt16(stackalloc bool[] { false, false, false, true }));
        Equal(ushort.MaxValue, BitVector.ToUInt16(CreateVector(16, true)));
    }

    [Fact]
    public static void UInt16ToBits()
    {
        var value = ushort.MaxValue;
        var buffer = new bool[16];

        BitVector.FromUInt16(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromUInt16(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromUInt16(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt32()
    {
        Equal(0, BitVector.ToInt32(ReadOnlySpan<bool>.Empty));
        Equal(3, BitVector.ToInt32(stackalloc bool[] { true, true }));
        Equal(8, BitVector.ToInt32(stackalloc bool[] { false, false, false, true }));
        Equal(int.MaxValue, BitVector.ToInt32(CreateVector(31, true)));
        Equal(-1, BitVector.ToInt32(CreateVector(32, true)));
    }

    [Fact]
    public static void Int32ToBits()
    {
        int value = -1;
        var buffer = new bool[32];

        BitVector.FromInt32(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromInt32(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromInt32(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToUInt32()
    {
        Equal(0U, BitVector.ToUInt32(ReadOnlySpan<bool>.Empty));
        Equal(3U, BitVector.ToUInt32(stackalloc bool[] { true, true }));
        Equal(8U, BitVector.ToUInt32(stackalloc bool[] { false, false, false, true }));
        Equal(uint.MaxValue, BitVector.ToUInt32(CreateVector(32, true)));
    }

    [Fact]
    public static void UInt32ToBits()
    {
        var value = uint.MaxValue;
        var buffer = new bool[32];

        BitVector.FromUInt32(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromUInt32(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromUInt32(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt64()
    {
        Equal(0L, BitVector.ToInt64(ReadOnlySpan<bool>.Empty));
        Equal(3L, BitVector.ToInt64(stackalloc bool[] { true, true }));
        Equal(8L, BitVector.ToInt64(stackalloc bool[] { false, false, false, true }));
        Equal(long.MaxValue, BitVector.ToInt64(CreateVector(63, true)));
        Equal(-1L, BitVector.ToInt32(CreateVector(64, true)));
    }

    [Fact]
    public static void Int64ToBits()
    {
        long value = -1;
        var buffer = new bool[64];

        BitVector.FromInt64(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromInt64(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromInt64(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToUInt64()
    {
        Equal(0UL, BitVector.ToUInt64(ReadOnlySpan<bool>.Empty));
        Equal(3UL, BitVector.ToUInt64(stackalloc bool[] { true, true }));
        Equal(8UL, BitVector.ToUInt64(stackalloc bool[] { false, false, false, true }));
        Equal(ulong.MaxValue, BitVector.ToUInt64(CreateVector(64, true)));
    }

    [Fact]
    public static void UInt64ToBits()
    {
        var value = ulong.MaxValue;
        var buffer = new bool[64];

        BitVector.FromUInt64(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.FromUInt64(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.FromUInt64(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }
}