using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AsyncCountdownEventTests : Test
{
    [Fact]
    public static async Task Counting()
    {
        using var countdown = new AsyncCountdownEvent(4);
        False(countdown.IsSet);
        False(await countdown.WaitAsync(TimeSpan.FromMilliseconds(100)));

        False(countdown.Signal()); //count == 3
        False(countdown.IsSet);
        False(await countdown.WaitAsync(TimeSpan.FromMilliseconds(100)));

        True(countdown.Signal(3));
        True(countdown.IsSet);
        True(await countdown.WaitAsync(TimeSpan.FromMilliseconds(40)));
    }

    [Theory]
    [InlineData(0L, 0L, false)]
    [InlineData(1L, 0L, false)]
    [InlineData(128L, 0L, false)]
    [InlineData(1024L * 1024L, 0L, false)]
    [InlineData(1L, 1024L, false)]
    [InlineData(128L, 1024L, false)]
    [InlineData(1024L * 1024, 1024L, false)]
    [InlineData(1, 0, true)]
    [InlineData(128, 0, true)]
    [InlineData(1024 * 1024, 0, true)]
    [InlineData(1, 1024, true)]
    [InlineData(128, 1024, true)]
    [InlineData(1024 * 1024, 1024, true)]
    public static void CheckStateTransitions(long initCount, long increms, bool takeAllAtOnce)
    {
        using var ev = new AsyncCountdownEvent(initCount);
        Equal(initCount, ev.InitialCount);

        // Increment (optionally).
        for (var i = 1; i < increms + 1; i++)
        {
            ev.AddCount();
            Equal(initCount + i, ev.CurrentCount);
        }

        // Decrement until it hits 0.
        if (takeAllAtOnce)
            ev.Signal(initCount + increms);
        else
            for (int i = 0; i < initCount + increms; i++)
            {
                False(ev.IsSet);
                ev.Signal();
            }

        True(ev.IsSet);
        Equal(0, ev.CurrentCount);

        // Now reset the event and check its count.
        True(ev.Reset());
        Equal(ev.InitialCount, ev.CurrentCount);
    }
}