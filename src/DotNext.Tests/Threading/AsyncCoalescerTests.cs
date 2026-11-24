using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncCoalescerTests : Test
{
    [Fact]
    public static async Task CoalesceOnTimeout()
    {
        await using var coalescer = new TestCoalescer(TimeSpan.FromMilliseconds(100));
        var task1 = coalescer.WaitAsync(TestToken).AsTask();
        var task2 = coalescer.WaitAsync(TestToken).AsTask();

        False(await task1);
        False(await task2);
        Equal(1, coalescer.Invocations);
    }

    [Fact]
    public static async Task CoalesceManually()
    {
        await using var coalescer = new TestCoalescer(InfiniteTimeSpan);
        var task1 = coalescer.WaitAsync(TestToken).AsTask();
        var task2 = coalescer.WaitAsync(TestToken).AsTask();

        coalescer.Flush();
        True(await task1);
        True(await task2);
        Equal(1, coalescer.Invocations);

        // no waiters, no action
        coalescer.Flush();
        Equal(1, coalescer.Invocations);
    }

    [Fact]
    public static async Task LateCallerJoinsNextWindow()
    {
        await using var coalescer = new TestCoalescer(InfiniteTimeSpan);
        var gate = coalescer.Block();
        var early = coalescer.WaitAsync(TestToken).AsTask();
        coalescer.Flush();

        // the action is in progress, the late caller must not be resumed by it
        var late = coalescer.WaitAsync(TestToken).AsTask();
        gate.SetResult();
        True(await early);
        False(late.IsCompleted);

        coalescer.Flush();
        True(await late);
        Equal(2, coalescer.Invocations);
    }

    [Fact]
    public static async Task FailurePropagation()
    {
        await using var coalescer = new TestCoalescer(InfiniteTimeSpan) { Failure = new ArithmeticException() };
        var task = coalescer.WaitAsync(TestToken).AsTask();
        coalescer.Flush();
        await ThrowsAsync<ArithmeticException>(() => task);
    }

    [Fact]
    public static async Task CanceledCallerIsRemoved()
    {
        await using var coalescer = new TestCoalescer(InfiniteTimeSpan);
        using var source = new CancellationTokenSource();
        var canceled = coalescer.WaitAsync(source.Token).AsTask();
        var task = coalescer.WaitAsync(TestToken).AsTask();

        source.Cancel();
        await ThrowsAnyAsync<OperationCanceledException>(() => canceled);

        coalescer.Flush();
        True(await task);

        // the node returned to the pool is reused safely
        task = coalescer.WaitAsync(TestToken).AsTask();
        coalescer.Flush();
        True(await task);
    }

    [Fact]
    public static async Task StaleCanceledNodeInReusedBatch()
    {
        await using var coalescer = new TestCoalescer(InfiniteTimeSpan);
        using var source = new CancellationTokenSource();

        // keep ValueTask unconsumed, so the node is returned to the pool only when awaited
        var stale = coalescer.WaitAsync(source.Token);
        var first = coalescer.WaitAsync(TestToken).AsTask();
        source.Cancel();

        coalescer.Flush();
        True(await first); // the batch is drained and returned to the pool

        // the batch is reused by the next window
        var second = coalescer.WaitAsync(TestToken).AsTask();
        var third = coalescer.WaitAsync(TestToken).AsTask();

        // the stale node removes itself from the reused batch
        await ThrowsAnyAsync<OperationCanceledException>(stale.AsTask);

        coalescer.Flush();
        True(await second);
        True(await third);
    }

    [Fact]
    public static async Task Disposal()
    {
        var coalescer = new TestCoalescer(InfiniteTimeSpan);
        var task = coalescer.WaitAsync(TestToken).AsTask();

        await coalescer.DisposeAsync();
        await ThrowsAsync<ObjectDisposedException>(() => task);
        await ThrowsAsync<ObjectDisposedException>(coalescer.WaitAsync(TestToken).AsTask);
        coalescer.Flush(); // no-op
        await coalescer.DisposeAsync(); // idempotent
    }
}

file sealed class TestCoalescer(TimeSpan delay) : AsyncCoalescer(delay, TimeProvider.System)
{
    private volatile TaskCompletionSource gate;
    private volatile int invocations;

    public int Invocations => invocations;

    public Exception Failure;

    // blocks the action until the gate is open
    public TaskCompletionSource Block()
        => gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task CompleteAsync(bool triggeredManually)
    {
        Interlocked.Increment(ref invocations);
        if (gate is { } g)
            await g.Task;

        if (Failure is { } e)
            throw e;
    }
}