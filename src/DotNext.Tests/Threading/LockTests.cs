namespace DotNext.Threading;

public sealed class LockTests : Test
{
    [Fact]
    public static void EmptyLock()
    {
        var @lock = default(Lock);
        False(@lock.TryAcquire(out var holder));
        if (holder)
            Fail("Lock is acquired");

        holder = @lock.Acquire();
        if (holder)
            Fail("Lock is acquired");

        Throws<TimeoutException>(() => @lock.Acquire(DefaultTimeout));

        False(@lock.TryAcquire(DefaultTimeout, out holder));

        holder.Dispose();
    }

    [Fact]
    public static void MonitorLock()
    {
        var syncRoot = new object();
        using var @lock = Lock.Monitor(syncRoot);
        True(@lock.TryAcquire(out var holder));
        True(Monitor.IsEntered(syncRoot));
        holder.Dispose();
        False(Monitor.IsEntered(syncRoot));

        holder = @lock.Acquire(DefaultTimeout);
        True(Monitor.IsEntered(syncRoot));
        holder.Dispose();
        False(Monitor.IsEntered(syncRoot));
    }
    
    [Fact]
    public static void ExclusiveLock()
    {
        var syncRoot = new System.Threading.Lock();
        using var @lock = Lock.ExclusiveLock(syncRoot);
        True(@lock.TryAcquire(out var holder));
        True(syncRoot.IsHeldByCurrentThread);
        holder.Dispose();
        False(syncRoot.IsHeldByCurrentThread);

        holder = @lock.Acquire(DefaultTimeout);
        True(syncRoot.IsHeldByCurrentThread);
        holder.Dispose();
        False(syncRoot.IsHeldByCurrentThread);
    }

    [Fact]
    public static void SemaphoreLock()
    {
        using var sem = new SemaphoreSlim(3);
        using var @lock = Lock.Semaphore(sem);
        True(@lock.TryAcquire(out var holder));
        Equal(2, sem.CurrentCount);
        holder.Dispose();
        Equal(3, sem.CurrentCount);
    }
    
    [Fact]
    public static async Task InterruptibleLock2()
    {
        using var cts = new CancellationTokenSource();
        var @lock = new System.Threading.Lock();
        
        @lock.Enter();
        var task = Task.Factory.StartNew(() => @lock.TryEnter(System.Threading.Timeout.InfiniteTimeSpan, cts.Token), TaskCreationOptions.LongRunning);
        await cts.CancelAsync();
        False(await task);
    }
    
    [Fact]
    public static void ReaderWriterLock()
    {
        var obj = new object();
        var holder1 = Lock.AcquireReadLock(obj, DefaultTimeout);
        if (holder1) { }
        else Fail("Lock is not acquired");

        var holder2 = Lock.AcquireReadLock(obj, DefaultTimeout);
        if (holder2) { }
        else Fail("Lock is not acquired");

        Throws<LockRecursionException>(() => Lock.AcquireWriteLock(obj, TimeSpan.Zero));
        holder1.Dispose();
        holder2.Dispose();

        holder1 = Lock.AcquireWriteLock(obj, TimeSpan.Zero);
        if (holder1) { }
        else Fail("Lock is not acquired");
        holder1.Dispose();
    }
    
    [Fact]
    public static void InvalidLock()
    {
        var obj = string.Intern("Interned string");
        Throws<InvalidOperationException>(() => Lock.AcquireReadLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireWriteLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, TimeSpan.Zero));
    }
}