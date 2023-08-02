namespace DotNext.Threading;

public sealed class AsyncBridgeTests : Test
{
    [Fact]
    public static async Task WaitForCancellationNoThrow()
    {
        using var source = new CancellationTokenSource(400);
        await source.Token.WaitAsync();
        True(source.IsCancellationRequested);
    }

    [Fact]
    public static async Task WaitForCancellation()
    {
        using var source = new CancellationTokenSource(400);
        await ThrowsAsync<OperationCanceledException>(source.Token.WaitAsync(true).AsTask);
        True(source.IsCancellationRequested);
    }

    [Fact]
    public static async Task WaitForSignal()
    {
        using var ev = new ManualResetEvent(false);
        ThreadPool.QueueUserWorkItem(static state => state.Set(), ev, false);
        await ev.WaitAsync(DefaultTimeout);
    }

    [Fact]
    public static async Task AlreadySignaled()
    {
        using var ev = new ManualResetEvent(true);
        True(await ev.WaitAsync(DefaultTimeout));
    }

    [Fact]
    public static async Task PoolOverflow()
    {
        var tokens = new CancellationTokenSource[AsyncBridge.MaxPoolSize + 1];
        var tasks = new Task[tokens.Length];

        for (var i = 0; i < tokens.Length; i++)
        {
            tasks[i] = (tokens[i] = new()).Token.WaitAsync().AsTask();
        }

        using var source1 = new CancellationTokenSource();
        using var source2 = new CancellationTokenSource();

        var task1 = source1.Token.WaitAsync(completeAsCanceled: true).AsTask();
        var task2 = source2.Token.WaitAsync(completeAsCanceled: false).AsTask();

        source1.Cancel();
        source2.Cancel();
        await ThrowsAnyAsync<OperationCanceledException>(Func.Constant(task1));
        await task2;

        foreach (var token in tokens)
            token.Cancel();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public static async Task CancellationTokenAwaitCornerCases()
    {
        await ThrowsAnyAsync<OperationCanceledException>(new CancellationToken(true).WaitAsync(completeAsCanceled: true).AsTask);
        await new CancellationToken(true).WaitAsync();
        await ThrowsAsync<ArgumentException>(new CancellationToken(false).WaitAsync().AsTask);
    }
}