using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    public readonly struct AsyncLock : IDisposable
    {
        private enum LockType : byte
        {
            None = 0,
            Exclusive,
            Semaphore
        }

        private const TaskContinuationOptions ContinuationOptions = TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion;

        private readonly object lockedObject;
        private readonly LockType type;
        private readonly bool owner;

        private AsyncLock(object lockedObject, LockType type, bool owner)
        {
            this.lockedObject = lockedObject;
            this.type = type;
            this.owner = owner;
        }

        public static AsyncLock Exclusive() => new AsyncLock(new AsyncLockOwner(), LockType.Exclusive, true);

        public static AsyncLock Semaphore(SemaphoreSlim semaphore) => new AsyncLock(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), LockType.Semaphore, false);

        public static AsyncLock Semaphore(int initialCount, int maxCount) => new AsyncLock(new SemaphoreSlim(initialCount, maxCount), LockType.Semaphore, true);

        private static void CheckOnTimeout(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        public Task Acquire(CancellationToken token) => TryAcquire(TimeSpan.MaxValue, default).ContinueWith(CheckOnTimeout, ContinuationOptions);

        public Task Acquire(TimeSpan timeout) => TryAcquire(timeout).ContinueWith(CheckOnTimeout, ContinuationOptions);

        public Task<bool> TryAcquire(TimeSpan timeout) => TryAcquire(timeout, default);

        public Task<bool> TryAcquire(TimeSpan timeout, CancellationToken token)
        {
            switch(type)
            {
                case LockType.Exclusive:
                    return As<AsyncLockOwner>(lockedObject).TryAcquire(timeout, token);
                case LockType.Semaphore:
                    return As<SemaphoreSlim>(lockedObject).WaitAsync(timeout, token);
                default:
                    return CompletedTask<bool, BooleanConst.False>.Task;
            }
        }

        /// <summary>
        /// Releases acquired lock.
        /// </summary>
        public void Release()
        {
            switch (type)
            {
                case LockType.Exclusive:
                    As<AsyncLockOwner>(lockedObject).Release();
                    return;
                case LockType.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Release(1);
                    return;
            }
        }

        internal void DestroyUnderlyingLock()
        {
            if (owner)
                (lockedObject as IDisposable)?.Dispose();
        }

        void IDisposable.Dispose() => Release();
    }
}