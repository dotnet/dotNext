namespace DotNext.Threading;

public sealed class TimeoutSourceTests : Test
{
    [Fact]
    public static void ImmediateCancellation()
    {
        using var source = new TimeoutSource(TimeProvider.System, new(canceled: true));
        True(source.IsCanceled);
        False(source.IsTimedOut);
        True(source.Token.IsCancellationRequested);
        False(source.TryStart(DefaultTimeout));
        False(source.TryReset());
    }

    [Fact]
    public static async Task ImmediateTimeout()
    {
        await using var source = new TimeoutSource(TimeProvider.System, new(canceled: false));
        True(source.TryStart(TimeSpan.Zero));

        await source.Token.WaitAsync();
        True(source.IsTimedOut);
        False(source.TryReset());
    }

    [Fact]
    public static void Cancellation()
    {
        using var cts = new CancellationTokenSource();
        using var source = new TimeoutSource(TimeProvider.System, cts.Token);
        False(source.IsCanceled);
        False(source.IsTimedOut);
        
        cts.Cancel();
        True(source.Token.IsCancellationRequested);
        True(source.IsCanceled);
        False(source.IsTimedOut);
    }

    [Fact]
    public static void IdempotentReset()
    {
        using var source = new TimeoutSource(TimeProvider.System, new(canceled: false));
        True(source.TryReset());
        True(source.TryReset());
    }
}