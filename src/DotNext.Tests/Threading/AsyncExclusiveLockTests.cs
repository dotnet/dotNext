using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Timeout;

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
            var task = new TaskCompletionSource<bool>();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                False(await @lock.TryAcquireAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                await @lock.AcquireAsync(DefaultTimeout);
                @lock.Release();
                task.SetResult(true);
            });
            True(are.WaitOne(DefaultTimeout));
            @lock.Release();
            await task.Task;
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
    }
}
