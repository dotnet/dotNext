namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncReaderWriterLockTests : Test
{
    [Fact]
    public static async Task TrivialLock()
    {
        using var rwLock = new AsyncReaderWriterLock { ConcurrencyLevel = 3 };

        // read lock
        True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
        True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
        False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
        rwLock.Release();
        False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
        rwLock.Release();

        // write lock
        True(await rwLock.TryEnterWriteLockAsync(DefaultTimeout));
        False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(20)));
        rwLock.Release();

        // upgrade to write lock
        True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
        True(await rwLock.TryUpgradeToWriteLockAsync(DefaultTimeout));
        False(rwLock.TryEnterWriteLock());
        Throws<SynchronizationLockException>(() => rwLock.TryUpgradeToWriteLock());
        rwLock.DowngradeFromWriteLock();
        True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
    }

    [Fact]
    public static async Task WriterToWriterChain()
    {
        var are = new TaskCompletionSource();
        using var rwLock = new AsyncReaderWriterLock();
        True(await rwLock.TryEnterWriteLockAsync(TimeSpan.Zero));
        var task = Task.Run(async () =>
        {
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(10)));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            await rwLock.EnterWriteLockAsync(DefaultTimeout);
            rwLock.Release();
        });

        await are.Task.WaitAsync(DefaultTimeout);
        rwLock.Release();
        await task.WaitAsync(DefaultTimeout);
    }

    [Fact]
    public static async Task WriterToReaderChain()
    {
        var are = new TaskCompletionSource();
        using var rwLock = new AsyncReaderWriterLock();
        await rwLock.EnterWriteLockAsync(DefaultTimeout);
        var task = Task.Run(async () =>
        {
            False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            await rwLock.EnterReadLockAsync(DefaultTimeout);
            rwLock.Release();
        });

        await are.Task.WaitAsync(DefaultTimeout);
        rwLock.Release();
        await task.WaitAsync(DefaultTimeout);
    }

    [Fact]
    public static void OptimisticRead()
    {
        using var rwLock = new AsyncReaderWriterLock();
        var stamp = rwLock.TryOptimisticRead();
        True(rwLock.Validate(stamp));
        True(rwLock.TryEnterReadLock());
        Equal(1, rwLock.CurrentReadCount);
        True(rwLock.Validate(stamp));
        rwLock.Release();
        Equal(stamp, rwLock.TryOptimisticRead());
        True(rwLock.TryEnterWriteLock(stamp));
        False(rwLock.IsReadLockHeld);
        True(rwLock.IsWriteLockHeld);
        False(rwLock.Validate(stamp));
    }

    [Fact]
    public static void CallDisposeTwice()
    {
        var @lock = new AsyncReaderWriterLock();
        @lock.Dispose();
        True(@lock.DisposeAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public static void DisposeAsyncCompletedSynchronously()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.DisposeAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public static async Task GracefulShutdown()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.TryEnterWriteLock());
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);
        @lock.Release();
        await task;
        Throws<ObjectDisposedException>(() => @lock.TryEnterReadLock());
    }

    [Fact]
    public static async Task GracefulShutdown2()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.TryEnterReadLock());
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);
        await ThrowsAnyAsync<ObjectDisposedException>(@lock.EnterWriteLockAsync().AsTask);
        @lock.Release();
        await task;
    }

    [Fact]
    public static async Task GracefulShutdown3()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.TryEnterWriteLock());
        var acquisition1 = @lock.EnterReadLockAsync();
        False(acquisition1.IsCompleted);
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);

        await ThrowsAnyAsync<ObjectDisposedException>(@lock.EnterReadLockAsync().AsTask);

        @lock.Release();
        await acquisition1;
        False(task.IsCompleted);

        @lock.Release();
        await task;
    }

    [Fact]
    public static async Task QueueFairness()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.TryEnterReadLock());

        var writeLock = @lock.EnterWriteLockAsync();
        var readLock = @lock.EnterReadLockAsync();
        False(writeLock.IsCompleted);
        False(readLock.IsCompleted);

        @lock.Release();
        await writeLock;

        @lock.Release();
        await readLock;
    }

    [Fact]
    public static async Task LockStealing()
    {
        const string reason = "Hello, world!";
        using var @lock = new AsyncReaderWriterLock();
        True(await @lock.TryEnterWriteLockAsync(DefaultTimeout));

        var task1 = @lock.TryEnterWriteLockAsync(DefaultTimeout).AsTask();
        var task2 = @lock.TryEnterReadLockAsync(DefaultTimeout).AsTask();
        var task3 = @lock.TryStealWriteLockAsync(reason, DefaultTimeout).AsTask();

        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task1))).Reason);
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task2))).Reason);

        @lock.Release();
        True(await task3);
    }

    [Fact]
    public static async Task LockStealing2()
    {
        const string reason = "Hello, world!";
        using var @lock = new AsyncReaderWriterLock();
        True(await @lock.TryEnterWriteLockAsync(DefaultTimeout));

        var task1 = @lock.TryEnterWriteLockAsync(DefaultTimeout).AsTask();
        var task2 = @lock.TryEnterReadLockAsync(DefaultTimeout).AsTask();
        var task3 = @lock.StealWriteLockAsync(reason, DefaultTimeout).AsTask();

        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task1))).Reason);
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(Func.Constant(task2))).Reason);

        @lock.Release();
        await task3;
    }
    
    [Fact]
    public static async Task DisposedWhenSynchronousReadLockAcquired()
    {
        var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock());

        var t = Task.Factory.StartNew(() => l.TryEnterWriteLock(DefaultTimeout), TaskCreationOptions.LongRunning);
        
        l.Dispose();
        await ThrowsAsync<ObjectDisposedException>(Func.Constant(t));
    }
    
    [Fact]
    public static async Task DisposedWhenSynchronousWriteLockAcquired()
    {
        var l = new AsyncReaderWriterLock();
        True(l.TryEnterWriteLock());

        var t = Task.Factory.StartNew(() => l.TryEnterReadLock(DefaultTimeout), TaskCreationOptions.LongRunning);
        
        l.Dispose();
        await ThrowsAsync<ObjectDisposedException>(Func.Constant(t));
    }

    [Fact]
    public static async Task AcquireReadWriteLockSynchronously()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock(DefaultTimeout));
        Equal(1L, l.CurrentReadCount);

        var t = Task.Factory.StartNew(() => l.TryEnterWriteLock(DefaultTimeout), TaskCreationOptions.LongRunning);
        
        l.Release();

        True(await t);
        True(l.IsWriteLockHeld);
        
        l.Release();
        False(l.IsWriteLockHeld);
    }

    [Fact]
    public static async Task ResumeMultipleReadersSynchronously()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterWriteLock());

        var t1 = Task.Factory.StartNew(TryEnterReadLock, TaskCreationOptions.LongRunning);
        var t2 = Task.Factory.StartNew(TryEnterReadLock, TaskCreationOptions.LongRunning);
        
        l.Release();
        Equal(new[] { true, true }, await Task.WhenAll(t1, t2));
        Equal(2L, l.CurrentReadCount);

        bool TryEnterReadLock() => l.TryEnterReadLock(DefaultTimeout);
    }
    
    [Fact]
    public static void ReentrantLock()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock());

        Throws<LockRecursionException>(() => l.TryEnterReadLock(DefaultTimeout));
        Throws<LockRecursionException>(() => l.TryEnterWriteLock(DefaultTimeout));
        
        l.Release();
        True(l.TryEnterReadLock(DefaultTimeout));
    }

    [Fact]
    public static async Task NoDeadlockWhenUpgrade()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterReadLockAsync();
        await l.EnterReadLockAsync();

        var task1 = l.UpgradeToWriteLockAsync().AsTask(); // suspends
        False(task1.IsCompleted);

        var task2 = l.UpgradeToWriteLockAsync().AsTask();
        False(task2.IsCompleted);
        await task1;
        l.Release();

        await task2;
        l.Release();
    }

    [Fact]
    public static void UpgradeToWriteLock()
    {
        using var l = new AsyncReaderWriterLock();
        Throws<SynchronizationLockException>(() => l.TryUpgradeToWriteLock());
        
        True(l.TryEnterReadLock());
        True(l.IsReadLockHeld);
        False(l.IsWriteLockHeld);
        
        True(l.TryUpgradeToWriteLock());
        False(l.IsReadLockHeld);
        True(l.IsWriteLockHeld);
        
        l.Release();
        False(l.IsReadLockHeld);
        False(l.IsWriteLockHeld);
        
        True(l.TryEnterReadLock());
        True(l.TryEnterReadLock());
        False(l.TryUpgradeToWriteLock());
        Equal(1L, l.CurrentReadCount);
    }
    
    [Fact]
    public static async Task UpgradeToWriteLockAsync()
    {
        await using var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock());

        var task1 = l.EnterWriteLockAsync().AsTask();
        False(task1.IsCompleted);

        var task2 = l.UpgradeToWriteLockAsync().AsTask();
        False(task2.IsCompleted);
        
        await task1;
        False(task2.IsCompleted);
        l.Release();

        await task2;
        l.Release();
    }
}