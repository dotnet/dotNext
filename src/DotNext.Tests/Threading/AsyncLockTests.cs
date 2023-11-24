namespace DotNext.Threading;

public sealed class AsyncLockTests : Test
{
    [Fact]
    public static async Task EmptyLock()
    {
        var @lock = default(AsyncLock);

        var holder = await @lock.AcquireAsync(CancellationToken.None);
        if (holder)
            Fail("Lock is in acquired state");

        holder = await @lock.AcquireAsync(DefaultTimeout);
        if (holder)
            Fail("Lock is in acquired state");

        holder.Dispose();
    }

    [Fact]
    public static async Task ExclusiveLock()
    {
        using var syncRoot = new AsyncExclusiveLock();
        using var @lock = AsyncLock.Exclusive(syncRoot);
        var holder = await @lock.TryAcquireAsync(DefaultTimeout, CancellationToken.None);
        if (holder) { }
        else Fail("Lock was not acquired");
        True(syncRoot.IsLockHeld);
        holder.Dispose();
        False(syncRoot.IsLockHeld);

        holder = await @lock.AcquireAsync(DefaultTimeout, CancellationToken.None);
        True(syncRoot.IsLockHeld);
        holder.Dispose();
        False(syncRoot.IsLockHeld);
    }

    [Fact]
    public static async Task SemaphoreLock()
    {
        using var sem = new SemaphoreSlim(3);
        using var @lock = AsyncLock.Semaphore(sem);
        var holder = await @lock.TryAcquireAsync(DefaultTimeout, CancellationToken.None);
        if (holder) { }
        else Fail("Lock was not acquired");
        Equal(2, sem.CurrentCount);
        holder.Dispose();
        Equal(3, sem.CurrentCount);
    }
}