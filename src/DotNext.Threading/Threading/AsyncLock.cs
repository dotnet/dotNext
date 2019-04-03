using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    public readonly struct AsyncLock : IDisposable, IEquatable<AsyncLock>
    {
        private enum LockType : byte
        {
            None = 0,
            Exclusive,
            ReadLock,
            UpgradableReadLock,
            WriteLock,
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

        public static AsyncLock ReadLock(AsyncReaderWriterLock rwLock) => new AsyncLock(rwLock, LockType.ReadLock, false);

        public static AsyncLock UpgradableReadLock(AsyncReaderWriterLock rwLock) => new AsyncLock(rwLock, LockType.UpgradableReadLock, false);

        public static AsyncLock WriteLock(AsyncReaderWriterLock rwLock) => new AsyncLock(rwLock, LockType.WriteLock, false);

        private static void CheckOnTimeout(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        public Task Acquire(CancellationToken token) => TryAcquire(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, ContinuationOptions);

        public Task Acquire(TimeSpan timeout) => TryAcquire(timeout).ContinueWith(CheckOnTimeout, ContinuationOptions);

        public Task<bool> TryAcquire(TimeSpan timeout) => TryAcquire(timeout, default);

        public Task<bool> TryAcquire(TimeSpan timeout, CancellationToken token)
        {
            switch(type)
            {
                case LockType.Exclusive:
                    return As<AsyncLockOwner>(lockedObject).TryAcquire(timeout, token);
                case LockType.ReadLock:
                    return As<AsyncReaderWriterLock>(lockedObject).TryEnterReadLock(timeout, token);
                case LockType.UpgradableReadLock:
                    return As<AsyncReaderWriterLock>(lockedObject).TryEnterUpgradableReadLock(timeout, token);
                case LockType.WriteLock:
                    return As<AsyncReaderWriterLock>(lockedObject).TryEnterWriteLock(timeout, token);
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
                case LockType.ReadLock:
                    As<AsyncReaderWriterLock>(lockedObject).ExitReadLock();
                    return;
                case LockType.WriteLock:
                    As<AsyncReaderWriterLock>(lockedObject).ExitWriteLock();
                    return;
                case LockType.UpgradableReadLock:
                    As<AsyncReaderWriterLock>(lockedObject).ExitUpgradableReadLock();
                    return;
                case LockType.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Release(1);
                    return;
            }
        }

        internal void DestroyUnderlyingLock()
        {
            if (owner && lockedObject is IDisposable disposable)
                disposable.Dispose();
        }

        void IDisposable.Dispose() => Release();

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public bool Equals(AsyncLock other) => type == other.type && ReferenceEquals(lockedObject, other.lockedObject) && owner == other.owner;

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is AsyncLock @lock && Equals(@lock);

        /// <summary>
        /// Computes hash code of this lock.
        /// </summary>
        /// <returns>The hash code of this lock.</returns>
        public override int GetHashCode()
        {
            if (lockedObject is null)
                return 0;
            var hashCode = -549183179;
            hashCode = hashCode * -1521134295 + lockedObject.GetHashCode();
            hashCode = hashCode * -1521134295 + type.GetHashCode();
            hashCode = hashCode * -1521134295 + owner.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Returns actual type of this lock in the form of the string.
        /// </summary>
        /// <returns>The actual type of this lock.</returns>
        public override string ToString() => type.ToString();

        /// <summary>
        /// Determines whether two locks are the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in AsyncLock first, in AsyncLock second) => ReferenceEquals(first.lockedObject, second.lockedObject) && first.type == second.type && first.owner == second.owner;

        /// <summary>
        /// Determines whether two locks are not the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in AsyncLock first, in AsyncLock second) => !ReferenceEquals(first.lockedObject, second.lockedObject) || first.type != second.type || first.owner != second.owner;
    }
}