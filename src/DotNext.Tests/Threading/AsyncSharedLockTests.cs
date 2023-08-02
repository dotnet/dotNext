namespace DotNext.Threading;

public sealed class AsyncSharedLockTests : Test
{
    [Fact]
    public static async Task WeakLocks()
    {
        using var sharedLock = new AsyncSharedLock(3);
        Equal(3, sharedLock.ConcurrencyLevel);
        True(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
        True(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
        Equal(1, sharedLock.RemainingCount);
        True(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
        Equal(0, sharedLock.RemainingCount);
        False(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
        False(await sharedLock.TryAcquireAsync(true, TimeSpan.Zero));
        sharedLock.Release();
        Equal(1, sharedLock.RemainingCount);
        False(await sharedLock.TryAcquireAsync(true, TimeSpan.Zero));
        True(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
    }

    [Fact]
    public static async Task StrongLocks()
    {
        using var sharedLock = new AsyncSharedLock(3, false);
        True(await sharedLock.TryAcquireAsync(true, TimeSpan.Zero));
        False(await sharedLock.TryAcquireAsync(false, TimeSpan.Zero));
        False(await sharedLock.TryAcquireAsync(true, TimeSpan.Zero));
    }

    private static async void AcquireWeakLockAndRelease(AsyncSharedLock sharedLock, AsyncCountdownEvent acquireEvent)
    {
        await Task.Delay(100);
        await sharedLock.AcquireAsync(false, TimeSpan.Zero);
        acquireEvent.Signal();
        await Task.Delay(100);
        sharedLock.Release();
    }

    [Fact]
    public static async Task WeakToStrongLockTransition()
    {
        using var acquireEvent = new AsyncCountdownEvent(3L);
        using var sharedLock = new AsyncSharedLock(3);
        AcquireWeakLockAndRelease(sharedLock, acquireEvent);
        AcquireWeakLockAndRelease(sharedLock, acquireEvent);
        AcquireWeakLockAndRelease(sharedLock, acquireEvent);
        True(await acquireEvent.WaitAsync(DefaultTimeout));
        await sharedLock.AcquireAsync(true, DefaultTimeout);
    }

    private static async void AcquireWeakLock(AsyncSharedLock sharedLock, AsyncCountdownEvent acquireEvent)
    {
        await sharedLock.AcquireAsync(false, DefaultTimeout, CancellationToken.None);
        acquireEvent.Signal();
    }

    [Fact]
    public static async Task StrongToWeakLockTransition()
    {
        using var acquireEvent = new AsyncCountdownEvent(2L);
        using var sharedLock = new AsyncSharedLock(3);
        await sharedLock.AcquireAsync(true, TimeSpan.Zero);
        AcquireWeakLock(sharedLock, acquireEvent);
        AcquireWeakLock(sharedLock, acquireEvent);
        sharedLock.Release();
        True(await acquireEvent.WaitAsync(DefaultTimeout));
    }

    [Fact]
    public static void FailFastLock()
    {
        using var sharedLock = new AsyncSharedLock(3);
        True(sharedLock.TryAcquire(false));
        True(sharedLock.TryAcquire(false));
        True(sharedLock.TryAcquire(false));
        False(sharedLock.TryAcquire(true));
        False(sharedLock.TryAcquire(false));
        sharedLock.Release();
        sharedLock.Release();
        sharedLock.Release();
        True(sharedLock.TryAcquire(true));
        False(sharedLock.TryAcquire(false));
    }

    [Fact]
    public static void DowngradeFromStrongToWeakLock()
    {
        using var sharedLock = new AsyncSharedLock(3);
        True(sharedLock.TryAcquire(true));
        True(sharedLock.IsLockHeld);
        True(sharedLock.IsStrongLockHeld);
        Equal(0, sharedLock.RemainingCount);
        False(sharedLock.TryAcquire(false));
        sharedLock.Downgrade();
        Equal(2, sharedLock.RemainingCount);
        False(sharedLock.IsStrongLockHeld);
        True(sharedLock.IsLockHeld);
        sharedLock.Release();
        Equal(3, sharedLock.RemainingCount);
    }

    [Fact]
    public static void CallDisposeTwice()
    {
        var @lock = new AsyncSharedLock(3);
        @lock.Dispose();
        True(@lock.DisposeAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public static void DisposeAsyncCompletedAsynchronously()
    {
        using var @lock = new AsyncSharedLock(3);
        True(@lock.DisposeAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public static async Task GracefulShutdown()
    {
        using var @lock = new AsyncSharedLock(3);
        True(@lock.TryAcquire(false));
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);
        @lock.Release();
        await task;
        Throws<ObjectDisposedException>(() => @lock.TryAcquire(true));
    }

    [Fact]
    public static async Task GracefulShutdown2()
    {
        using var @lock = new AsyncSharedLock(3);
        True(@lock.TryAcquire(false));
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);
        await ThrowsAsync<ObjectDisposedException>(@lock.AcquireAsync(true, CancellationToken.None).AsTask);
        @lock.Release();
        await task;
    }

    [Fact]
    public static async Task GracefulShutdown3()
    {
        using var @lock = new AsyncSharedLock(3);
        True(@lock.TryAcquire(false));
        var task = @lock.DisposeAsync();
        False(task.IsCompleted);
        await ThrowsAsync<ObjectDisposedException>(@lock.AcquireAsync(true, CancellationToken.None).AsTask);
        @lock.Downgrade();
        False(task.IsCompleted);
        @lock.Release();
        await task;
    }

    [Fact]
    public static async Task QueueFairness()
    {
        using var @lock = new AsyncSharedLock(3);
        True(@lock.TryAcquire(false));

        var writeLockTask = @lock.AcquireAsync(true);
        var readLockTask = @lock.AcquireAsync(false);
        False(writeLockTask.IsCompleted);
        False(readLockTask.IsCompleted);

        @lock.Release();
        await writeLockTask;

        @lock.Release();
        await readLockTask;
    }
}