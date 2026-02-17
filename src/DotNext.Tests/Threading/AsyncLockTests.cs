using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncLockTests : Test
{
    [Fact]
    public static async Task EmptyLock()
    {
        var @lock = default(AsyncLock);

        var holder = await @lock.AcquireAsync(CancellationToken.None);
        if (holder)
            Fail("Lock is in acquired state");

        holder = await @lock.AcquireAsync(TestToken);
        if (holder)
            Fail("Lock is in acquired state");

        holder.Dispose();
    }

    [Fact]
    public static async Task ExclusiveLock()
    {
        using var syncRoot = new AsyncExclusiveLock();
        using var @lock = AsyncLock.Exclusive(syncRoot);
        var holder = await @lock.TryAcquireAsync(InfiniteTimeSpan, TestToken);
        if (holder) { }
        else Fail("Lock was not acquired");
        True(syncRoot.IsLockHeld);
        holder.Dispose();
        False(syncRoot.IsLockHeld);

        holder = await @lock.AcquireAsync(InfiniteTimeSpan, TestToken);
        True(syncRoot.IsLockHeld);
        holder.Dispose();
        False(syncRoot.IsLockHeld);
    }

    [Fact]
    public static async Task SemaphoreLock()
    {
        using var sem = new SemaphoreSlim(3);
        using var @lock = AsyncLock.Semaphore(sem);
        var holder = await @lock.TryAcquireAsync(InfiniteTimeSpan, TestToken);
        if (holder) { }
        else Fail("Lock was not acquired");
        Equal(2, sem.CurrentCount);
        holder.Dispose();
        Equal(3, sem.CurrentCount);
    }
}