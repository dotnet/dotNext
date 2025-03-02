namespace DotNext.Collections.Specialized;

public sealed class EytzingerArrayTests : Test
{
    private static ReadOnlySpan<int> TestData => [1, 5, 10, 15, 20, 25, 30, 35];

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(19, 20)]
    [InlineData(5, 10)]
    [InlineData(36, null)]
    public static void FindUpperBound(int value, int? upperBound)
    {
        using var array = new EytzingerArray<int>(TestData);

        Equal(upperBound.ToOptional(), array.FindUpperBound(value));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(19, 15)]
    [InlineData(36, 35)]
    [InlineData(11, 10)]
    public static void FindLowerBound(int value, int? lowerBound)
    {
        using var array = new EytzingerArray<int>(TestData);

        Equal(lowerBound.ToOptional(), array.FindLowerBound(value));
    }

    [Theory]
    [InlineData(-1, null, 1)]
    [InlineData(19, 15, 20)]
    [InlineData(36, 35, null)]
    [InlineData(11, 10, 15)]
    public static void FindBounds(int value, int? lowerBound, int? upperBound)
    {
        using var array = new EytzingerArray<int>(TestData);

        var (l, u) = array.FindBounds(value);
        Equal(lowerBound.ToOptional(), l);
        Equal(upperBound.ToOptional(), u);
    }

    [Fact]
    public static void PrepareArray()
    {
        Span<int> buffer = stackalloc int[TestData.Length + 1];
        EytzingerArray<int>.Prepare(TestData, buffer);

        var (lowerBound, upperBound) = EytzingerArray.FindBounds(buffer, 16);
        Equal(TestData[3], lowerBound.Value);
        Equal(TestData[4], upperBound.Value);
    }

    [Fact]
    public static void FromUnsortedData()
    {
        Span<int> buffer = stackalloc int[TestData.Length];
        TestData.CopyTo(buffer);
        Random.Shared.Shuffle(buffer);

        using var array = EytzingerArray.CreateFromUnsortedSpan<int>(buffer);
        
        var bound = array.FindLowerBound(16);
        Equal(TestData[3], bound.Value);

        bound = array.FindUpperBound(16);
        Equal(TestData[4], bound.Value);
    }
}