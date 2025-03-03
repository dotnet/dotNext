namespace DotNext.Numerics;

public sealed class FastModTests : Test
{
    [Theory]
    [InlineData(10U, 5U)]
    [InlineData(9U, 5U)]
    [InlineData(8U, 5U)]
    [InlineData(44U, 42U)]
    public static void CheckRemainders(uint dividend, uint divisor)
    {
        var fastMod = new FastMod(divisor);
        Equal(dividend % divisor, fastMod.GetRemainder(dividend));
    }
}