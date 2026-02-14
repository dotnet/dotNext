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
        Equal(0, byte.FromBits([]));
        Equal(3, byte.FromBits([true, true]));
        Equal(8, byte.FromBits([false, false, false, true]));
        Equal(byte.MaxValue, byte.FromBits(CreateVector(8, true)));
    }

    [Fact]
    public static void ByteToBits()
    {
        var value = byte.MaxValue;
        var buffer = new bool[8];

        value.GetBits(buffer);
        True(Array.TrueForAll(buffer, static bit => bit));

        value = 3;
        Array.Clear(buffer);
        value.GetBits(buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        value.GetBits(buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt16()
    {
        Equal(0, short.FromBits([]));
        Equal(3, short.FromBits([true, true]));
        Equal(8, short.FromBits([false, false, false, true]));
        Equal(short.MaxValue, short.FromBits(CreateVector(15, true)));
        Equal(-1, short.FromBits(CreateVector(16, true)));
    }

    [Fact]
    public static void StressTest()
    {
        Span<bool> bits = stackalloc bool[32];
        Random.Shared.GetItems([true, false], bits);
        Equal(FromBitsSlow(bits), int.FromBits(bits));

        static int FromBitsSlow(ReadOnlySpan<bool> bits)
        {
            var result = 0;
            for (var position = 0; position < bits.Length; position++)
            {
                if (bits[position])
                    result |= 1 << position;
            }

            return result;
        }
    }

    [Fact]
    public static void Int16ToBits()
    {
        short value = -1;
        var buffer = new bool[16];

        value.GetBits(buffer);
        True(Array.TrueForAll(buffer, static bit => bit));

        value = 3;
        Array.Clear(buffer);
        value.GetBits(buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        value.GetBits(buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt32()
    {
        Equal(0, int.FromBits([]));
        Equal(3, int.FromBits([true, true]));
        Equal(8, int.FromBits([false, false, false, true]));
        Equal(int.MaxValue, int.FromBits(CreateVector(31, true)));
        Equal(-1, int.FromBits(CreateVector(32, true)));
    }

    [Fact]
    public static void Int32ToBits()
    {
        int value = -1;
        var buffer = new bool[32];

        value.GetBits(buffer);
        True(Array.TrueForAll(buffer, static bit => bit));

        value = 3;
        Array.Clear(buffer);
        value.GetBits(buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        value.GetBits(buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);
    }

    [Fact]
    public static void BitsToInt64()
    {
        Equal(0L, long.FromBits([]));
        Equal(3L, long.FromBits([true, true]));
        Equal(8L, long.FromBits([false, false, false, true]));
        Equal(long.MaxValue, long.FromBits(CreateVector(63, true)));
        Equal(-1L, long.FromBits(CreateVector(64, true)));
    }

    [Fact]
    public static void Int64ToBits()
    {
        long value = -1;
        var buffer = new bool[64];

        value.GetBits(buffer);
        True(Array.TrueForAll(buffer, static bit => bit));

        value = 3;
        Array.Clear(buffer);
        value.GetBits(buffer);
        True(buffer[0]);
        True(buffer[1]);
        False(buffer[2]);

        value = 8;
        Array.Clear(buffer);
        value.GetBits(buffer);
        False(buffer[0]);
        False(buffer[1]);
        False(buffer[2]);
        True(buffer[3]);

        value = 1L << 62;
        Array.Clear(buffer);
        value.GetBits(buffer);
        True(buffer[62]);
    }

    [Fact]
    public static void SingleBitManipulation()
    {
        var value = 0.SetBit(1, true);
        Equal(2, value);
        
        True(value.IsBitSet(1));
        False(value.IsBitSet(0));

        value = value.SetBit(1, false);
        False(value.IsBitSet(1));
    }
}