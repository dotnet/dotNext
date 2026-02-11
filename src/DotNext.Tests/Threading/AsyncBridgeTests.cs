namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
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
        await ev.WaitAsync(TestToken);
    }

    [Fact]
    public static async Task CancelWaitForSignal()
    {
        using var ev = new ManualResetEvent(false);
        using var cts = new CancellationTokenSource();

        var task = ev.WaitAsync(cts.Token).AsTask();
        await cts.CancelAsync();

        var e = await ThrowsAsync<OperationCanceledException>(task);
        Equal(cts.Token, e.CancellationToken);
    }

    [Fact]
    public static async Task AlreadySignaled()
    {
        using var ev = new ManualResetEvent(true);
        True(await ev.WaitAsync(DefaultTimeout, TestToken));
        True(ev.WaitAsync(TestToken).IsCompletedSuccessfully);
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

        await source1.CancelAsync();
        await source2.CancelAsync();
        await ThrowsAnyAsync<OperationCanceledException>(task1);
        await task2;

        foreach (var token in tokens)
            await token.CancelAsync();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public static async Task CancellationTokenAwaitCornerCases()
    {
        await ThrowsAnyAsync<OperationCanceledException>(new CancellationToken(true).WaitAsync(completeAsCanceled: true).AsTask);
        await new CancellationToken(true).WaitAsync();
        await ThrowsAsync<ArgumentException>(new CancellationToken(false).WaitAsync().AsTask);
    }

    [Fact]
    public static void CompletedTaskAsToken()
    {
        var token = Task.CompletedTask.AsCancellationToken();
        True(token.IsCancellationRequested);

        token = Task.CompletedTask.AsCancellationToken(out var disposeSource);
        True(token.IsCancellationRequested);
        False(disposeSource());
    }

    [Fact]
    public static async Task TaskAsToken()
    {
        var source = new TaskCompletionSource();
        var token = source.Task.AsCancellationToken();
        False(token.IsCancellationRequested);

        source.SetResult();
        await token.WaitAsync();
    }

    [Fact]
    public static void DisposeTaskTokenBeforeCompletion()
    {
        var source = new TaskCompletionSource();
        var token = source.Task.AsCancellationToken(out var disposeTokenSource);
        False(token.IsCancellationRequested);

        True(disposeTokenSource());
        source.SetResult();
    }
    
    [Fact]
    public static async Task DisposeTaskTokenAfterCompletion()
    {
        var source = new TaskCompletionSource();
        var token = source.Task.AsCancellationToken(out var disposeTokenSource);
        False(token.IsCancellationRequested);
        
        source.SetResult();
        await token.WaitAsync();
        False(disposeTokenSource());
    }
}