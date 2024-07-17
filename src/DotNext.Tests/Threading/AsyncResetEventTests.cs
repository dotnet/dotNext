namespace DotNext.Threading;

using Diagnostics;

public sealed class AsyncResetEventTests : Test
{
    [Fact]
    public static async Task ManualResetEvent()
    {
        using IAsyncResetEvent resetEvent = new AsyncManualResetEvent(false);
        Equal(EventResetMode.ManualReset, resetEvent.ResetMode);
        False(resetEvent.IsSet);
        var t = Task.Run(async () =>
        {
            True(await resetEvent.WaitAsync(DefaultTimeout));
        });

        True(resetEvent.Signal());
        await t;
        True(resetEvent.Reset());
        False(resetEvent.IsSet);

        t = Task.Run(async () =>
        {
            True(await resetEvent.WaitAsync(DefaultTimeout));
        });

        True(resetEvent.Signal());
        await t;
        True(resetEvent.IsSet);
    }

    [Fact]
    public static async Task SetResetForManualEvent()
    {
        using IAsyncResetEvent mre = new AsyncManualResetEvent(false);
        False(await mre.WaitAsync(TimeSpan.Zero));
        True(mre.Signal());
        True(await mre.WaitAsync(TimeSpan.Zero));
        True(await mre.WaitAsync(TimeSpan.Zero));
        False(mre.Signal());
        True(await mre.WaitAsync(TimeSpan.Zero));
        True(mre.Reset());
        False(await mre.WaitAsync(TimeSpan.Zero));
    }

    [Fact]
    public static async Task AutoresetForManualEvent()
    {
        using var resetEvent = new AsyncManualResetEvent(false, 3);
        False(resetEvent.IsSet);
        var t = resetEvent.WaitAsync(DefaultTimeout);

        True(resetEvent.Set(true));
        await t;

        False(resetEvent.IsSet);
        False(await resetEvent.WaitAsync(TimeSpan.Zero));
    }

    [Fact]
    public static void AutoresetOfSignaledManualEvent()
    {
        using var resetEvent = new AsyncManualResetEvent(true);
        True(resetEvent.IsSet);
        False(resetEvent.Set());
        False(resetEvent.Set(true));
        False(resetEvent.IsSet);
    }

    [Fact]
    public static async Task SetResetForAutoEvent()
    {
        using IAsyncResetEvent are = new AsyncAutoResetEvent(false);
        Equal(EventResetMode.AutoReset, are.ResetMode);
        False(await are.WaitAsync(TimeSpan.Zero));
        True(are.Signal());
        True(await are.WaitAsync(TimeSpan.Zero));
        False(await are.WaitAsync(TimeSpan.Zero));
        True(are.Signal());
        True(are.Reset());
        False(await are.WaitAsync(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public static async Task RegressionIssue82()
    {
        using var ev = new AsyncAutoResetEvent(false);
        var start = new Timestamp();

        var producer = Task.Run(() =>
        {
            while (start.Elapsed < TimeSpan.FromSeconds(1))
                ev.Set();
        });

        var consumer = Task.Run(async () =>
        {
            while (!producer.IsCompleted)
                await ev.WaitAsync(TimeSpan.FromMilliseconds(1));
        });

        await producer;
        ev.Set();
        await consumer;
    }
}