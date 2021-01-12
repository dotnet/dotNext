using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
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
            using var sharedLock = new AsyncSharedLock(3);
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

            Equal(0, sharedLock.RemainingCount);
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
            Equal(1, sharedLock.RemainingCount);
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
            Equal(0, sharedLock.RemainingCount);
            False(sharedLock.TryAcquire(false));
            sharedLock.Downgrade();
            Equal(2, sharedLock.RemainingCount);
            sharedLock.Release();
            Equal(3, sharedLock.RemainingCount);
        }

        [Fact]
        public static void DowgradeWeakLock()
        {
            using var sharedLock = new AsyncSharedLock(3);
            True(sharedLock.TryAcquire(false));
            Equal(2, sharedLock.RemainingCount);
            sharedLock.Downgrade();
            False(sharedLock.IsLockHeld);
            Throws<SynchronizationLockException>(sharedLock.Downgrade);
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
        public static void GracefulShutdown()
        {
            using var @lock = new AsyncSharedLock(3);
            True(@lock.TryAcquire(false));
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            @lock.Release();
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(() => @lock.TryAcquire(true));
        }

        [Fact]
        public static void GracefulShutdown2()
        {
            using var @lock = new AsyncSharedLock(3);
            True(@lock.TryAcquire(false));
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition = @lock.AcquireAsync(true, CancellationToken.None);
            False(acquisition.IsCompleted);
            @lock.Release();
            True(task.IsCompletedSuccessfully);
            True(acquisition.IsFaulted);
            Throws<ObjectDisposedException>(acquisition.GetAwaiter().GetResult);
        }

        [Fact]
        public static void GracefulShutdown3()
        {
            using var @lock = new AsyncSharedLock(3);
            True(@lock.TryAcquire(false));
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition = @lock.AcquireAsync(true, CancellationToken.None);
            False(acquisition.IsCompleted);
            @lock.Downgrade();
            True(task.IsCompletedSuccessfully);
            True(acquisition.IsFaulted);
            Throws<ObjectDisposedException>(acquisition.GetAwaiter().GetResult);
        }

        [Fact]
        public static void GracefulShutdown4()
        {
            using var @lock = new AsyncSharedLock(3);
            True(@lock.TryAcquire(true));
            var acquisition1 = @lock.AcquireAsync(false, CancellationToken.None);
            False(acquisition1.IsCompleted);
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition2 = @lock.AcquireAsync(false, CancellationToken.None);
            False(task.IsCompleted);

            @lock.Release();
            True(acquisition1.IsCompletedSuccessfully);
            False(acquisition2.IsCompleted);
            False(task.IsCompleted);

            @lock.Release();
            True(acquisition2.IsFaulted);
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(acquisition2.GetAwaiter().GetResult);
        }

        [Fact]
        public static void GracefulShutdown5()
        {
            using var @lock = new AsyncSharedLock(3);
            True(@lock.TryAcquire(true));
            var acquisition1 = @lock.AcquireAsync(false, CancellationToken.None);
            False(acquisition1.IsCompleted);
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition2 = @lock.AcquireAsync(false, CancellationToken.None);
            False(task.IsCompleted);

            @lock.Downgrade();
            True(acquisition1.IsCompletedSuccessfully);
            False(acquisition2.IsCompleted);
            False(task.IsCompleted);

            @lock.Downgrade();
            True(acquisition2.IsFaulted);
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(acquisition2.GetAwaiter().GetResult);
        }
    }
}
