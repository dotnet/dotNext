using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncLockTests: Assert
    {
        [Fact]
        public void DestroyTest()
        {
            var @lock = AsyncLock.Semaphore(0, 1);
            NotEqual("None", @lock.ToString());
            @lock.Destroy();
            True(@lock.GetHashCode() == 0);
            Equal("None", @lock.ToString());
        }

        [Fact]
        public async Task TrivialLock()
        {
            var @lock = AsyncLock.Exclusive();
            try
            {
                True(await @lock.TryAcquire(TimeSpan.FromMilliseconds(10)));
                False(await @lock.TryAcquire(TimeSpan.FromMilliseconds(100)));
                await ThrowsAsync<TimeoutException>(() => @lock.Acquire(TimeSpan.FromMilliseconds(100)));
                @lock.Release();
                True(await @lock.TryAcquire(TimeSpan.FromMilliseconds(100)));
                @lock.Release();
            }
            finally
            {
                @lock.Destroy();
            }
        }

        [Fact]
        public async Task ConcurrentLock()
        {
            using (var are = new AutoResetEvent(false))
            {
                var @lock = AsyncLock.Exclusive();
                True(await @lock.TryAcquire(TimeSpan.Zero));
                var task = Task.Factory.StartNew(async () =>
                {
                    False(await @lock.TryAcquire(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await @lock.Acquire(TimeSpan.FromHours(1));
                    @lock.Release();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                are.WaitOne();
                @lock.Release();
                await task;
                @lock.Destroy();
            }
        }
    }
}
