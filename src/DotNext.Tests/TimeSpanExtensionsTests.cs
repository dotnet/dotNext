namespace DotNext;

public sealed class TimeSpanExtensionsTests : Test
{
    public static TheoryData<TimeSpan, TimeSpan, TimeSpan> MaxTestData =>
    [
        (TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)),
        (TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
        (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
        (TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromSeconds(1)),
        (TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
        (TimeSpan.FromSeconds(-1), TimeSpan.Zero, TimeSpan.Zero),
        (TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.Zero),
        (TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(-1)),
        (TimeSpan.MinValue, TimeSpan.MaxValue, TimeSpan.MaxValue),
    ];

    [Theory]
    [MemberData(nameof(MaxTestData))]
    public static void Max(TimeSpan x, TimeSpan y, TimeSpan expected)
    {
        Equal(expected, TimeSpan.Max(x, y));
    }

    public static TheoryData<TimeSpan, TimeSpan, TimeSpan> MinTestData =>
    [
        (TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)),
        (TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
        (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
        (TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.Zero),
        (TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.Zero),
        (TimeSpan.FromSeconds(-1), TimeSpan.Zero, TimeSpan.FromSeconds(-1)),
        (TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(-1)),
        (TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(-2)),
        (TimeSpan.MinValue, TimeSpan.MaxValue, TimeSpan.MinValue),
    ];

    [Theory]
    [MemberData(nameof(MinTestData))]
    public static void Min(TimeSpan x, TimeSpan y, TimeSpan expected)
    {
        Equal(expected, TimeSpan.Min(x, y));
    }
}
