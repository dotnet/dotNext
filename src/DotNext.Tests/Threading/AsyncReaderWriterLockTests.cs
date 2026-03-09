using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncReaderWriterLockTests : Test
{
    [Fact]
    public static async Task TrivialLock()
    {
        using var rwLock = new AsyncReaderWriterLock { ConcurrencyLevel = 3 };

        // read lock
        True(await rwLock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken));
        True(await rwLock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken));
        False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20), TestToken));
        rwLock.Release();
        False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20), TestToken));
        rwLock.Release();

        // write lock
        True(await rwLock.TryEnterWriteLockAsync(InfiniteTimeSpan, TestToken));
        False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(20), TestToken));
        rwLock.Release();

        // upgrade to write lock
        True(await rwLock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken));
        True(await rwLock.TryUpgradeToWriteLockAsync(InfiniteTimeSpan, TestToken));
        False(rwLock.TryEnterWriteLock());
        Throws<SynchronizationLockException>(() => rwLock.TryUpgradeToWriteLock());
        rwLock.DowngradeFromWriteLock();
        True(await rwLock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken));
    }

    [Fact]
    public static async Task WriterToWriterChain()
    {
        var are = new TaskCompletionSource();
        using var rwLock = new AsyncReaderWriterLock();
        True(await rwLock.TryEnterWriteLockAsync(TimeSpan.Zero, TestToken));
        var task = Task.Run(async () =>
        {
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(10), TestToken));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            await rwLock.EnterWriteLockAsync(InfiniteTimeSpan, TestToken);
            rwLock.Release();
        }, TestToken);

        await are.Task.WaitAsync(TestToken);
        rwLock.Release();
        await task.WaitAsync(TestToken);
    }

    [Fact]
    public static async Task WriterToReaderChain()
    {
        var are = new TaskCompletionSource();
        using var rwLock = new AsyncReaderWriterLock();
        await rwLock.EnterWriteLockAsync(InfiniteTimeSpan, TestToken);
        var task = Task.Run(async () =>
        {
            False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10), TestToken));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            await rwLock.EnterReadLockAsync(InfiniteTimeSpan, TestToken);
            rwLock.Release();
        }, TestToken);

        await are.Task.WaitAsync(TestToken);
        rwLock.Release();
        await task.WaitAsync(TestToken);
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
        await ThrowsAnyAsync<ObjectDisposedException>(@lock.EnterWriteLockAsync(TestToken).AsTask);
        @lock.Release();
        await task;
    }

    [Fact]
    public static async Task GracefulShutdown3()
    {
        using var @lock = new AsyncReaderWriterLock();
        True(@lock.TryEnterWriteLock());
        var acquisition1 = @lock.EnterReadLockAsync(TestToken);
        False(acquisition1.IsCompleted);
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);

        await ThrowsAnyAsync<ObjectDisposedException>(@lock.EnterReadLockAsync(TestToken).AsTask);

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

        var writeLock = @lock.EnterWriteLockAsync(TestToken);
        var readLock = @lock.EnterReadLockAsync(TestToken);
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
        True(await @lock.TryEnterWriteLockAsync(InfiniteTimeSpan, TestToken));

        var task1 = @lock.TryEnterWriteLockAsync(InfiniteTimeSpan, TestToken).AsTask();
        var task2 = @lock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken).AsTask();
        var task3 = @lock.TryStealWriteLockAsync(reason, InfiniteTimeSpan, TestToken).AsTask();

        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(task1)).Reason);
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(task2)).Reason);

        @lock.Release();
        True(await task3);
    }

    [Fact]
    public static async Task LockStealing2()
    {
        const string reason = "Hello, world!";
        using var @lock = new AsyncReaderWriterLock();
        True(await @lock.TryEnterWriteLockAsync(InfiniteTimeSpan, TestToken));

        var task1 = @lock.TryEnterWriteLockAsync(InfiniteTimeSpan, TestToken).AsTask();
        var task2 = @lock.TryEnterReadLockAsync(InfiniteTimeSpan, TestToken).AsTask();
        var task3 = @lock.StealWriteLockAsync(reason, InfiniteTimeSpan, TestToken).AsTask();

        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(task1)).Reason);
        Same(reason, (await ThrowsAsync<PendingTaskInterruptedException>(task2)).Reason);

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
        await ThrowsAsync<ObjectDisposedException>(t);
    }
    
    [Fact]
    public static async Task DisposedWhenSynchronousWriteLockAcquired()
    {
        var l = new AsyncReaderWriterLock();
        True(l.TryEnterWriteLock());

        var t = Task.Factory.StartNew(() => l.TryEnterReadLock(DefaultTimeout), TaskCreationOptions.LongRunning);
        
        l.Dispose();
        await ThrowsAsync<ObjectDisposedException>(t);
    }

    [Fact]
    public static async Task AcquireReadWriteLockSynchronously()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock(InfiniteTimeSpan, TestToken));
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

        bool TryEnterReadLock() => l.TryEnterReadLock(InfiniteTimeSpan, TestToken);
    }
    
    [Fact]
    public static void ReentrantLock()
    {
        using var l = new AsyncReaderWriterLock();
        True(l.TryEnterReadLock());

        Throws<LockRecursionException>(() => l.TryEnterReadLock(InfiniteTimeSpan, TestToken));
        Throws<LockRecursionException>(() => l.TryEnterWriteLock(InfiniteTimeSpan, TestToken));
        
        l.Release();
        True(l.TryEnterReadLock(InfiniteTimeSpan, TestToken));
    }

    [Fact]
    public static async Task NoDeadlockWhenUpgrade()
    {
        using var l = new AsyncReaderWriterLock();
        await l.EnterReadLockAsync(TestToken);
        await l.EnterReadLockAsync(TestToken);

        var task1 = l.UpgradeToWriteLockAsync(TestToken).AsTask(); // suspends
        False(task1.IsCompleted);

        var task2 = l.UpgradeToWriteLockAsync(TestToken).AsTask();
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

        var task1 = l.EnterWriteLockAsync(TestToken).AsTask();
        False(task1.IsCompleted);

        var task2 = l.UpgradeToWriteLockAsync(TestToken).AsTask();
        False(task2.IsCompleted);
        
        await task1;
        False(task2.IsCompleted);
        l.Release();

        await task2;
        l.Release();
    }
}