using static System.Threading.Timeout;

namespace DotNext.Threading;

public sealed class TimeoutTests : Test
{
    private static void InfiniteTest(Timeout timeout)
    {
        False(timeout.IsExpired);
        True(timeout.IsInfinite);
        if (timeout) Fail("Unexpected timeout value");
        Equal(InfiniteTimeSpan, timeout);
        True(timeout.TryGetRemainingTime(out var remainingTime));
        Equal(InfiniteTimeSpan, remainingTime);

        timeout.ThrowIfExpired(out var remaining);
        Equal(InfiniteTimeSpan, remaining);
    }

    [Fact]
    public static void DefaultValue()
    {
        InfiniteTest(Timeout.Infinite);
        InfiniteTest(new Timeout(InfiniteTimeSpan));
    }

    [Fact]
    public static void ExpiredTimeout()
    {
        True(Timeout.Expired.IsExpired);
        False(Timeout.Expired.IsInfinite);
    }
}