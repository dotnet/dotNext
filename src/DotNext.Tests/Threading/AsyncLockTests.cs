using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncLockTests : Assert
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

            holder = await @lock.AcquireAsync(TimeSpan.FromHours(1));
            if (holder)
                throw new Exception();

            holder.Dispose();
        }

        [Fact]
        public static async Task ExclusiveLock()
        {
            using var syncRoot = new AsyncExclusiveLock();
            using var @lock = AsyncLock.Exclusive(syncRoot);
            var holder = await @lock.TryAcquireAsync(CancellationToken.None);
            if (holder) { }
            else throw new Exception();
            True(syncRoot.IsLockHeld);
            holder.Dispose();
            False(syncRoot.IsLockHeld);

            holder = await @lock.AcquireAsync(CancellationToken.None);
            True(syncRoot.IsLockHeld);
            holder.Dispose();
            False(syncRoot.IsLockHeld);
        }

        [Fact]
        public static async Task SemaphoreLock()
        {
            using var sem = new SemaphoreSlim(3);
            using var @lock = AsyncLock.Semaphore(sem);
            var holder = await @lock.TryAcquireAsync(CancellationToken.None);
            if (holder) { }
            else throw new Exception();
            Equal(2, sem.CurrentCount);
            holder.Dispose();
            Equal(3, sem.CurrentCount);
        }

        private sealed class DummyLock
        {
            private TaskCompletionSource<bool> state;

            internal bool IsLockHeld => state != null;

            private async Task<Func<Task>> AcquireAsync(TimeSpan timeout, CancellationToken token)
            {
                await Tasks.Synchronization.WaitAsync(state.Task, timeout, token);
                state = new TaskCompletionSource<bool>();
                return Release;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal Task<Func<Task>> TryAcquireAsync(TimeSpan timeout, CancellationToken token)
            {
                if (state is null)
                {
                    state = new TaskCompletionSource<bool>();
                    return Task.FromResult<Func<Task>>(Release);
                }
                return AcquireAsync(timeout, token);
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            private Task Release()
            {
                state?.TrySetResult(true);
                state = null;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public static async Task CustomLock()
        {
            var lockManager = new DummyLock();
            using var customLock = new AsyncLock(lockManager.TryAcquireAsync);
            False(lockManager.IsLockHeld);
            using (await customLock.AcquireAsync(TimeSpan.Zero))
            {
                True(lockManager.IsLockHeld);
            }
            False(lockManager.IsLockHeld);
            using (await customLock.TryAcquireAsync(TimeSpan.Zero))
            {
                True(lockManager.IsLockHeld);
            }
            False(lockManager.IsLockHeld);
        }
    }
}
