namespace DotNext.Threading;

public sealed class LinkedCancellationTokenSourceTests : Test
{
    [Fact]
    public static async Task LinkedCancellation()
    {
        using var source1 = new CancellationTokenSource();
        using var source2 = new CancellationTokenSource();
        var token = source1.Token;
        using var linked = token.LinkTo(source2.Token);
        NotNull(linked);

        source1.CancelAfter(100);
        try
        {
            await Task.Delay(DefaultTimeout, linked.Token);
        }
        catch (OperationCanceledException e)
        {
            Equal(e.CancellationToken, linked.Token);
            NotEqual(source1.Token, e.CancellationToken);
            Equal(linked.CancellationOrigin, source1.Token);
        }
    }

    [Fact]
    public static async Task DirectCancellation()
    {
        using var source1 = new CancellationTokenSource();
        using var source2 = new CancellationTokenSource();
        var token = source1.Token;
        using var linked = token.LinkTo(source2.Token);
        NotNull(linked);

        linked.CancelAfter(100);
        try
        {
            await Task.Delay(DefaultTimeout, linked.Token);
        }
        catch (OperationCanceledException e)
        {
            Equal(e.CancellationToken, linked.Token);
            NotEqual(source1.Token, e.CancellationToken);
            Equal(linked.CancellationOrigin, linked.Token);
        }
    }

    [Fact]
    public static async Task CancellationWithTimeout()
    {
        using var source1 = new CancellationTokenSource();
        var token = new CancellationToken(canceled: false);
        using var cts = token.LinkTo(DefaultTimeout, source1.Token);
        NotNull(cts);
        source1.Cancel();

        await token.WaitAsync();
    }

    [Fact]
    public static async Task ConcurrentCancellation()
    {
        using var source1 = new CancellationTokenSource();
        using var source2 = new CancellationTokenSource();
        using var source3 = new CancellationTokenSource();
        var token = source3.Token;

        using var cts = token.LinkTo([source1.Token, source2.Token]);
        NotNull(cts);
        var task1 = source1.CancelAsync();
        var task2 = source2.CancelAsync();
        var task3 = source3.CancelAsync();

        await token.WaitAsync();

        Contains(cts.CancellationOrigin, new[] { source1.Token, source2.Token, source3.Token });
        
        await Task.WhenAll(task1, task2, task3);
    }
}