namespace DotNext.Numerics;

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
        Equal(0, BitVector.FromBits<byte>([]));
        Equal(3, BitVector.FromBits<byte>([true, true]));
        Equal(8, BitVector.FromBits<byte>([false, false, false, true]));
        Equal(byte.MaxValue, BitVector.FromBits<byte>(CreateVector(8, true)));
    }

    [Fact]
    public static void ByteToBits()
    {
        var value = byte.MaxValue;
        var buffer = new bool[8];

        BitVector.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt16()
    {
        Equal(0, BitVector.FromBits<short>([]));
        Equal(3, BitVector.FromBits<short>([true, true]));
        Equal(8, BitVector.FromBits<short>([false, false, false, true]));
        Equal(short.MaxValue, BitVector.FromBits<short>(CreateVector(15, true)));
        Equal(-1, BitVector.FromBits<short>(CreateVector(16, true)));
    }

    [Fact]
    public static void Int16ToBits()
    {
        short value = -1;
        var buffer = new bool[16];

        BitVector.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt32()
    {
        Equal(0, BitVector.FromBits<int>([]));
        Equal(3, BitVector.FromBits<int>([true, true]));
        Equal(8, BitVector.FromBits<int>([false, false, false, true]));
        Equal(int.MaxValue, BitVector.FromBits<int>(CreateVector(31, true)));
        Equal(-1, BitVector.FromBits<int>(CreateVector(32, true)));
    }

    [Fact]
    public static void Int32ToBits()
    {
        int value = -1;
        var buffer = new bool[32];

        BitVector.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt64()
    {
        Equal(0L, BitVector.FromBits<long>([]));
        Equal(3L, BitVector.FromBits<long>([true, true]));
        Equal(8L, BitVector.FromBits<long>([false, false, false, true]));
        Equal(long.MaxValue, BitVector.FromBits<long>(CreateVector(63, true)));
        Equal(-1L, BitVector.FromBits<long>(CreateVector(64, true)));
    }

    [Fact]
    public static void Int64ToBits()
    {
        long value = -1;
        var buffer = new bool[64];

        BitVector.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);

        value = 1L << 62;
        Array.Clear(buffer);
        BitVector.GetBits(value, buffer);
        True(buffer[62]);
    }
}