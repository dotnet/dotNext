namespace DotNext.Threading;

partial class AsyncExclusiveLockTests
{
    [Fact]
    public static async Task YieldWithoutSuspendedCallers()
    {
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        True(l.YieldAsync(TestToken).IsCompletedSuccessfully);
        True(l.IsLockHeld);
        l.Release();
    }

    [Fact]
    public static async Task YieldPassesLockToSuspendedCaller()
    {
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        var waiter1 = l.AcquireAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();
        await waiter1.WaitAsync(DefaultTimeout, TestToken);

        // the yielding caller is at the head of the queue
        var waiter2 = l.AcquireAsync(TestToken).AsTask();
        False(yielder.IsCompleted);

        l.Release(); // released by waiter1
        await yielder.WaitAsync(DefaultTimeout, TestToken);
        False(waiter2.IsCompleted);

        l.Release(); // released by yielder
        await waiter2.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldBehindCanceledCaller()
    {
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        using var cts = new CancellationTokenSource();
        var canceled = l.AcquireAsync(cts.Token); // not consumed, so the node stays in the queue
        await cts.CancelAsync();

        True(l.YieldAsync(TestToken).IsCompletedSuccessfully);
        True(l.IsLockHeld);

        await ThrowsAnyAsync<OperationCanceledException>(canceled.AsTask);
        True(l.IsLockHeld);
        l.Release();
    }

    [Fact]
    public static async Task YieldWithHardConcurrencyLimit()
    {
        using var l = new AsyncExclusiveLock
        {
            ConcurrencyLevel = 1L,
            HasConcurrencyLimit = true,
        };

        await l.AcquireAsync(TestToken);
        var waiter = l.AcquireAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();

        await waiter.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
        await yielder.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldWithCanceledToken()
    {
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        await ThrowsAnyAsync<OperationCanceledException>(l.YieldAsync(new CancellationToken(canceled: true)).AsTask);
        False(l.IsLockHeld);
    }

    [Fact]
    public static async Task CancelYield()
    {
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        using var cts = new CancellationTokenSource();
        var waiter = l.AcquireAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(cts.Token).AsTask();
        await waiter.WaitAsync(DefaultTimeout, TestToken);

        await cts.CancelAsync();
        await ThrowsAnyAsync<OperationCanceledException>(yielder);

        // the lock is still held by the waiter
        True(l.IsLockHeld);
        l.Release();
        False(l.IsLockHeld);
    }

    [Fact]
    public static async Task InterruptYield()
    {
        const string reason = "Hello, world!";
        using var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        var waiter = l.AcquireAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();
        await waiter.WaitAsync(DefaultTimeout, TestToken);

        var stealer = l.StealAsync(reason, TestToken).AsTask();
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(yielder)).Reason);

        l.Release(); // released by waiter
        await stealer.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldDuringGracefulShutdown()
    {
        var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        var disposeTask = l.DisposeAsync().AsTask();
        False(disposeTask.IsCompleted);

        await ThrowsAnyAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
        False(l.IsLockHeld);
        await disposeTask.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task YieldDuringGracefulShutdownWithSuspendedCaller()
    {
        var l = new AsyncExclusiveLock();
        await l.AcquireAsync(TestToken);

        var waiter = l.AcquireAsync(TestToken).AsTask();
        var disposeTask = l.DisposeAsync().AsTask();

        await ThrowsAnyAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
        await waiter.WaitAsync(DefaultTimeout, TestToken);
        False(disposeTask.IsCompleted);

        l.Release(); // released by waiter
        await disposeTask.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task YieldAfterDispose()
    {
        var l = new AsyncExclusiveLock();
        l.Dispose();

        await ThrowsAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
    }

    [Fact]
    public static async Task YieldWithoutLock()
    {
        using var l = new AsyncExclusiveLock();

        await ThrowsAsync<SynchronizationLockException>(l.YieldAsync(TestToken).AsTask);
    }

    [Fact]
    public static void YieldSynchronouslyAcquiredLock()
    {
        using var l = new AsyncExclusiveLock();
        True(l.TryAcquire(TimeSpan.Zero, TestToken));

        var task = l.YieldAsync(TestToken).AsTask();
        True(task.IsCompleted);
        IsType<InvalidOperationException>(task.Exception?.InnerException);
        True(l.IsLockHeld);
        l.Release();
    }
}