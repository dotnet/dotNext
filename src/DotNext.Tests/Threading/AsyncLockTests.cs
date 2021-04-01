using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncLockTests : Test
    {
        [Fact]
        public static async Task EmptyLock()
        {
            var @lock = default(AsyncLock);
            var holder = await @lock.TryAcquireAsync(CancellationToken.None);
            if (holder)
                throw new Exception();

            holder = await @lock.AcquireAsync(CancellationToken.None);
            if (holder)
                throw new Exception();

            holder = await @lock.AcquireAsync(DefaultTimeout);
            if (holder)
                throw new Exception();

            holder.Dispose();
        }

        [Fact]
        public static async Task ExclusiveLock()
        {
            using var syncRoot = new AsyncExclusiveLock();
            using var @lock = AsyncLock.Exclusive(syncRoot);
            var holder = await @lock.TryAcquireAsync(DefaultTimeout, CancellationToken.None);
            if (holder) { }
            else throw new Exception();
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
            else throw new Exception();
            Equal(2, sem.CurrentCount);
            holder.Dispose();
            Equal(3, sem.CurrentCount);
        }

        [Fact]
        public static void DisposedState()
        {
            var l = AsyncLock.Exclusive();
            l.Dispose();
            var result = l.TryAcquireAsync(CancellationToken.None).SuppressDisposedState();
            True(result.IsCompletedSuccessfully);
            False(result.Result);
        }
    }
}
