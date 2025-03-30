namespace DotNext.Numerics;

public sealed class NumberTests : Test
{
    [Fact]
    public static void NormalizeToFloatingPoint()
    {
        Equal(1F, int.MaxValue.Normalize<int, float>(int.MinValue, int.MaxValue));
        Equal(-1F, int.MinValue.Normalize<int, float>(int.MinValue, int.MaxValue));

        Equal(1D, int.MaxValue.Normalize<int, double>(int.MinValue, int.MaxValue));
        Equal(-1D, int.MinValue.Normalize<int, double>(int.MinValue, int.MaxValue));
    }

    [Fact]
    public static void WeightOfUInt64()
    {
        var weight = 0UL.Normalize();
        Equal(0D, weight);

        weight = ulong.MaxValue.Normalize();
        Equal(0.9999999999999999D, weight);

        weight = (ulong.MaxValue - 1UL).Normalize();
        Equal(0.9999999999999998D, weight);
    }

    [Fact]
    public static void WeightOfInt64()
    {
        Equal(unchecked((ulong)long.MaxValue).Normalize(), long.MaxValue.Normalize());
    }

    [Fact]
    public static void WeightOfUInt32()
    {
        var weight = 0U.Normalize();
        Equal(0F, weight);

        weight = uint.MaxValue.Normalize();
        Equal(0.99999994F, weight);

        weight = (uint.MaxValue - 1U).Normalize();
        Equal(0.9999999F, weight);
    }

    [Fact]
    public static void WeightOfInt32()
    {
        Equal(unchecked((uint)int.MaxValue).Normalize(), int.MaxValue.Normalize());
    }

    [Fact]
    public static void NumberType()
    {
        True(Number.IsSigned<int>());
        False(Number.IsSigned<uint>());
    }

    [Fact]
    public static void BinarySize()
    {
        Equal(sizeof(int), Number.GetMaxByteCount<int>());
        Equal(sizeof(long), Number.GetMaxByteCount<long>());
    }

    [Fact]
    public static void IsPrime()
    {
        False(1L.IsPrime());
        True(2L.IsPrime());
        True(Number.IsPrime<sbyte>(3));
        False(4.IsPrime());

        True(1669.IsPrime());
    }

    [Theory]
    [InlineData(8192U, 4097U, 4096U)]
    [InlineData(6U, 5U, 2U)]
    [InlineData(8U, 8U, 8U)]
    public static void RoundUp(uint expected, uint value, uint multiplier)
    {
        Equal(expected, value.RoundUp(multiplier));
    }
    
    [Theory]
    [InlineData(4096U, 4097U, 4096U)]
    [InlineData(4U, 5U, 2U)]
    [InlineData(8U, 8U, 8U)]
    public static void RoundDown(uint expected, uint value, uint multiplier)
    {
        Equal(expected, value.RoundDown(multiplier));
    }
}