namespace DotNext.Threading;

internal static class TimeoutExtensions
{
    internal static TimeSpan GetRemainingTimeOrZero(this in Timeout timeout)
    {
        if (!timeout.TryGetRemainingTime(out var remainingTime))
            remainingTime = TimeSpan.Zero;

        return remainingTime;
    }
}