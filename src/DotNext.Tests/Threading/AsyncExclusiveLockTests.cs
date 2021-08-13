using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncExclusiveLockTests : Test
    {
        [Fact]
        public static async Task TrivialLock()
        {
            using var @lock = new AsyncExclusiveLock();
            True(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(10)));
            False(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(100)));
            await ThrowsAsync<TimeoutException>(() => @lock.AcquireAsync(TimeSpan.FromMilliseconds(100)));
            @lock.Release();
            True(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public static async Task ConcurrentLock()
        {
            using var are = new AutoResetEvent(false);
            using var @lock = new AsyncExclusiveLock();
            await @lock.AcquireAsync(TimeSpan.Zero);
            var task = Task.Run(async () =>
            {
                False(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(static ev => ev.Set(), are, false));
                await @lock.AcquireAsync(DefaultTimeout);
                @lock.Release();
                return true;
            });
            True(are.WaitOne(DefaultTimeout));
            @lock.Release();
            True(await task);
        }

        [Fact]
        public static void FailFastLock()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            True(@lock.IsLockHeld);
            False(@lock.TryAcquire());
            @lock.Release();
        }

        [Fact]
        public static void CancelSuspendedCallers()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var waitNode = @lock.AcquireAsync(CancellationToken.None);
            False(waitNode.IsCompleted);
            Throws<ArgumentOutOfRangeException>(() => @lock.CancelSuspendedCallers(new CancellationToken(false)));
            @lock.CancelSuspendedCallers(new CancellationToken(true));
            True(waitNode.IsCompleted);
            False(waitNode.IsCompletedSuccessfully);
            True(waitNode.IsCanceled);
        }

        [Fact]
        public static void CallDisposeTwice()
        {
            var @lock = new AsyncExclusiveLock();
            @lock.Dispose();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void DisposeAsyncCompletedAsynchronously()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void GracefulShutdown()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            @lock.Release();
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(() => @lock.TryAcquire());
        }

        [Fact]
        public static void GracefulShutdown2()
        {
            using var @lock = new AsyncExclusiveLock();
            True(@lock.TryAcquire());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition = @lock.AcquireAsync(CancellationToken.None);
            False(acquisition.IsCompleted);
            @lock.Release();
            True(task.IsCompletedSuccessfully);
            True(acquisition.IsFaulted);
            Throws<ObjectDisposedException>(acquisition.GetAwaiter().GetResult);
        }

        [Fact]
        public static void DisposedState()
        {
            var l = new AsyncExclusiveLock();
            l.Dispose();
            var result = l.TryAcquireAsync(System.Threading.Timeout.InfiniteTimeSpan);
            True(result.IsFaulted);
            IsType<ObjectDisposedException>(result.Exception.InnerException);
        }
    }
}
