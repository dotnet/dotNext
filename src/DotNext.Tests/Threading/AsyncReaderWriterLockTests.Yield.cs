namespace DotNext.Threading;

partial class AsyncReaderWriterLockTests
{
    [Fact]
    public static async Task YieldWithoutSuspendedCallers()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        True(l.YieldAsync(TestToken).IsCompletedSuccessfully);
        True(l.IsWriteLockHeld);
        l.Release();
    }

    [Fact]
    public static async Task YieldPassesLockToSuspendedWriter()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        var writer1 = l.EnterWriteLockAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();
        await writer1.WaitAsync(DefaultTimeout, TestToken);

        // the yielding caller is at the head of the queue
        var writer2 = l.EnterWriteLockAsync(TestToken).AsTask();
        False(yielder.IsCompleted);

        l.Release(); // released by writer1
        await yielder.WaitAsync(DefaultTimeout, TestToken);
        False(writer2.IsCompleted);

        l.Release(); // released by yielder
        await writer2.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldPassesLockToSuspendedReaders()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        var reader1 = l.EnterReadLockAsync(TestToken).AsTask();
        var reader2 = l.EnterReadLockAsync(TestToken).AsTask();
        var writer = l.EnterWriteLockAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();

        await Task.WhenAll(reader1, reader2).WaitAsync(DefaultTimeout, TestToken);
        Equal(2L, l.CurrentReadCount);
        False(yielder.IsCompleted);

        var stamp = l.TryOptimisticRead();
        True(l.Validate(stamp));

        l.Release(); // released by reader1
        False(yielder.IsCompleted);
        l.Release(); // released by reader2

        // the yielding caller reacquires the write lock ahead of the suspended writer
        await yielder.WaitAsync(DefaultTimeout, TestToken);
        True(l.IsWriteLockHeld);
        False(writer.IsCompleted);
        False(l.Validate(stamp));

        l.Release(); // released by yielder
        await writer.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldBehindCanceledCaller()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        using var cts = new CancellationTokenSource();
        var canceled = l.EnterReadLockAsync(cts.Token); // not consumed, so the node stays in the queue
        await cts.CancelAsync();

        True(l.YieldAsync(TestToken).IsCompletedSuccessfully);
        True(l.IsWriteLockHeld);

        await ThrowsAnyAsync<OperationCanceledException>(canceled.AsTask);
        True(l.IsWriteLockHeld);
        l.Release();
    }

    [Fact]
    public static async Task YieldWithCanceledToken()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        await ThrowsAnyAsync<OperationCanceledException>(l.YieldAsync(new CancellationToken(canceled: true)).AsTask);
        False(l.IsWriteLockHeld);
    }

    [Fact]
    public static async Task CancelYield()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        using var cts = new CancellationTokenSource();
        var writer = l.EnterWriteLockAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(cts.Token).AsTask();
        await writer.WaitAsync(DefaultTimeout, TestToken);

        await cts.CancelAsync();
        await ThrowsAnyAsync<OperationCanceledException>(yielder);

        // the lock is still held by the writer
        True(l.IsWriteLockHeld);
        l.Release();
        False(l.IsWriteLockHeld);
    }

    [Fact]
    public static async Task InterruptYield()
    {
        const string reason = "Hello, world!";
        using var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        var writer = l.EnterWriteLockAsync(TestToken).AsTask();
        var yielder = l.YieldAsync(TestToken).AsTask();
        await writer.WaitAsync(DefaultTimeout, TestToken);

        var stealer = l.StealWriteLockAsync(reason, TestToken).AsTask();
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(yielder)).Reason);

        l.Release(); // released by writer
        await stealer.WaitAsync(DefaultTimeout, TestToken);
        l.Release();
    }

    [Fact]
    public static async Task YieldDuringGracefulShutdown()
    {
        var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        var disposeTask = l.DisposeAsync().AsTask();
        False(disposeTask.IsCompleted);

        await ThrowsAnyAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
        False(l.IsWriteLockHeld);
        await disposeTask.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task YieldDuringGracefulShutdownWithSuspendedCaller()
    {
        var l = new AsyncReaderWriterLock();
        await l.EnterWriteLockAsync(TestToken);

        var reader = l.EnterReadLockAsync(TestToken).AsTask();
        var disposeTask = l.DisposeAsync().AsTask();

        await ThrowsAnyAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
        await reader.WaitAsync(DefaultTimeout, TestToken);
        False(disposeTask.IsCompleted);

        l.Release(); // released by reader
        await disposeTask.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task YieldAfterDispose()
    {
        var l = new AsyncReaderWriterLock();
        l.Dispose();

        await ThrowsAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
    }

    [Fact]
    public static async Task YieldAfterDisposeWithSynchronousLock()
    {
        var l = new AsyncReaderWriterLock();
        True(l.TryEnterWriteLock(TimeSpan.Zero, TestToken));
        l.Dispose();

        await ThrowsAsync<ObjectDisposedException>(l.YieldAsync(TestToken).AsTask);
    }

    [Fact]
    public static async Task YieldWithoutLock()
    {
        using var l = new AsyncReaderWriterLock();

        await ThrowsAsync<SynchronizationLockException>(l.YieldAsync(TestToken).AsTask);
    }

    [Fact]
    public static async Task YieldReadLock()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterReadLockAsync(TestToken);

        await ThrowsAsync<SynchronizationLockException>(l.YieldAsync(TestToken).AsTask);
        Equal(1L, l.CurrentReadCount);
        l.Release();
    }

    [Fact]
    public static void YieldSynchronouslyAcquiredLock()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterWriteLock(TimeSpan.Zero, TestToken));

        var task = l.YieldAsync(TestToken).AsTask();
        True(task.IsCompleted);
        IsType<InvalidOperationException>(task.Exception?.InnerException);
        True(l.IsWriteLockHeld);
        l.Release();
    }
}
