using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class LockTests : Assert
    {
        [Fact]
        public static void EmptyLock()
        {
            var @lock = default(Lock);
            False(@lock.TryAcquire(out var holder));
            if (holder)
                throw new Exception();

            holder = @lock.Acquire();
            if (holder)
                throw new Exception();

            Throws<TimeoutException>(() => @lock.Acquire(TimeSpan.FromHours(1)));

            False(@lock.TryAcquire(TimeSpan.FromHours(1), out holder));

            holder.Dispose();
        }

        [Fact]
        public static void MonitorLock()
        {
            var syncRoot = new object();
            using (var @lock = Lock.Monitor(syncRoot))
            {
                True(@lock.TryAcquire(out var holder));
                True(Monitor.IsEntered(syncRoot));
                holder.Dispose();
                False(Monitor.IsEntered(syncRoot));

                holder = @lock.Acquire();
                True(Monitor.IsEntered(syncRoot));
                holder.Dispose();
                False(Monitor.IsEntered(syncRoot));
            }
        }

        [Fact]
        public static void SemaphoreLock()
        {
            using (var sem = new SemaphoreSlim(3))
            using (var @lock = Lock.Semaphore(sem))
            {
                True(@lock.TryAcquire(out var holder));
                Equal(2, sem.CurrentCount);
                holder.Dispose();
                Equal(3, sem.CurrentCount);
            }
        }
    }
}
