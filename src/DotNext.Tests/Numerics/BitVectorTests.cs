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
        Equal(0, Number.FromBits<byte>([]));
        Equal(3, Number.FromBits<byte>([true, true]));
        Equal(8, Number.FromBits<byte>([false, false, false, true]));
        Equal(byte.MaxValue, Number.FromBits<byte>(CreateVector(8, true)));
    }

    [Fact]
    public static void ByteToBits()
    {
        var value = byte.MaxValue;
        var buffer = new bool[8];

        Number.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt16()
    {
        Equal(0, Number.FromBits<short>([]));
        Equal(3, Number.FromBits<short>([true, true]));
        Equal(8, Number.FromBits<short>([false, false, false, true]));
        Equal(short.MaxValue, Number.FromBits<short>(CreateVector(15, true)));
        Equal(-1, Number.FromBits<short>(CreateVector(16, true)));
    }

    [Fact]
    public static void Int16ToBits()
    {
        short value = -1;
        var buffer = new bool[16];

        Number.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt32()
    {
        Equal(0, Number.FromBits<int>([]));
        Equal(3, Number.FromBits<int>([true, true]));
        Equal(8, Number.FromBits<int>([false, false, false, true]));
        Equal(int.MaxValue, Number.FromBits<int>(CreateVector(31, true)));
        Equal(-1, Number.FromBits<int>(CreateVector(32, true)));
    }

    [Fact]
    public static void Int32ToBits()
    {
        int value = -1;
        var buffer = new bool[32];

        Number.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt64()
    {
        Equal(0L, Number.FromBits<long>([]));
        Equal(3L, Number.FromBits<long>([true, true]));
        Equal(8L, Number.FromBits<long>([false, false, false, true]));
        Equal(long.MaxValue, Number.FromBits<long>(CreateVector(63, true)));
        Equal(-1L, Number.FromBits<long>(CreateVector(64, true)));
    }

    [Fact]
    public static void Int64ToBits()
    {
        long value = -1;
        var buffer = new bool[64];

        Number.GetBits(value, buffer);
        Array.TrueForAll(buffer, static bit => bit);

        value = 3;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);

        value = 1L << 62;
        Array.Clear(buffer);
        Number.GetBits(value, buffer);
        True(buffer[62]);
    }
}