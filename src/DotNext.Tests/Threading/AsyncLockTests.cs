using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncLockTests: Assert
    {
        private static bool IsAlive(in AsyncLock.Holder holder) => holder ? true : false;

        [Fact]
        public async Task TrivialLock()
        {
            using (var @lock = AsyncLock.Exclusive())
            {
                var holder = await @lock.TryAcquire(TimeSpan.FromMilliseconds(10));
                True(IsAlive(holder));
                False(IsAlive(await @lock.TryAcquire(TimeSpan.FromMilliseconds(100))));
                await ThrowsAsync<TimeoutException>(() => @lock.Acquire(TimeSpan.FromMilliseconds(100)));
                holder.Dispose();
                holder = await @lock.TryAcquire(TimeSpan.FromMilliseconds(100));
                True(IsAlive(holder));
                holder.Dispose();
            }
        }

        [Fact]
        public async Task ConcurrentLock()
        {
            using (var are = new AutoResetEvent(false))
            using (var @lock = AsyncLock.Exclusive())
            {
                var holder = await @lock.TryAcquire(TimeSpan.Zero);
                True(IsAlive(holder));
                var task = Task.Factory.StartNew(async () =>
                {
                    False(IsAlive(await @lock.TryAcquire(TimeSpan.FromMilliseconds(10))));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    (await @lock.Acquire(TimeSpan.FromHours(1))).Dispose();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                are.WaitOne();
                holder.Dispose();
                await task;
            }
        }
    }
}
