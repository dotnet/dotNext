using System;
using System.Threading;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents unified representation monitor lock, read lock,
    /// write lock or upgradable read lock.
    /// </summary>
    /// <remarks>
    /// The lock acquisition may block the caller thread.
    /// If you're looking for non-blocking asynchronous locks then try <see cref="AsyncLock"/>.
    /// </remarks>
    public readonly struct Lock : IDisposable, IEquatable<Lock>
    {
        private enum LockType : byte
        {
            None = 0,
            Monitor,
            ReadLock,
            UpgradableReadLock,
            WriteLock,
            Semaphore
        }

        private readonly object lockedObject;
        private readonly LockType type;
        private readonly bool owner;

        private Lock(object lockedObject, LockType type, bool owner)
        {
            this.lockedObject = lockedObject;
            this.type = type;
            this.owner = owner;
        }

        /// <summary>
        /// Creates semaphore-based lock but doesn't acquire lock.
        /// </summary>
        /// <param name="semaphore">The semaphore to wrap into lock object.</param>
        /// <returns>The lock representing semaphore.</returns>
        public static Lock Semaphore(SemaphoreSlim semaphore) => new Lock(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), LockType.Semaphore, false);

        public static Lock Semaphore(int initialCount, int maxCount) => new Lock(new SemaphoreSlim(initialCount, maxCount), LockType.Semaphore, true);

        /// <summary>
        /// Creates monitor-based lock control object but doesn't acquire lock.
        /// </summary>
        /// <param name="obj">Monitor lock target.</param>
        /// <returns>The lock representing monitor.</returns>
        public static Lock Monitor(object obj) => new Lock(obj ?? throw new ArgumentNullException(nameof(obj)), LockType.Monitor, false);

        /// <summary>
        /// Creates exclusive lock.
        /// </summary>
        /// <returns>The exclusive lock.</returns>
        public static Lock Monitor() => new Lock(new object(), LockType.Monitor, true);

        /// <summary>
        /// Creates read lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Read-only lock.</returns>
        public static Lock ReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.ReadLock, false);

        /// <summary>
        /// Creates write lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Write-only lock.</returns>
        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.WriteLock, false);

        /// <summary>
        /// Creates upgradable read lock but doesn't acquire.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Upgradable read lock.</returns>
        public static Lock UpgradableReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.UpgradableReadLock, false);

        /// <summary>
        /// Acquires lock.
        /// </summary>
        public void Acquire()
        {
            switch (type)
            {
                case LockType.Monitor:
                    System.Threading.Monitor.Enter(lockedObject);
                    return;
                case LockType.ReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterReadLock();
                    return;
                case LockType.WriteLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterWriteLock();
                    return;
                case LockType.UpgradableReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterUpgradeableReadLock();
                    return;
                case LockType.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Wait();
                    return;
            }
        }

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/></returns>
        public bool TryAcquire()
        {
            switch (type)
            {
                case LockType.Monitor:
                    return System.Threading.Monitor.TryEnter(lockedObject);
                case LockType.ReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(0);
                case LockType.WriteLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(0);
                case LockType.UpgradableReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(0);
                case LockType.Semaphore:
                    return As<SemaphoreSlim>(lockedObject).Wait(0);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the lock</param>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/></returns>
        public bool TryAcquire(TimeSpan timeout)
        {
            switch (type)
            {
                case LockType.Monitor:
                    return System.Threading.Monitor.TryEnter(lockedObject, timeout);
                case LockType.ReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(timeout);
                case LockType.WriteLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(timeout);
                case LockType.UpgradableReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(timeout);
                case LockType.Semaphore:
                    return As<SemaphoreSlim>(lockedObject).Wait(timeout);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Releases acquired lock.
        /// </summary>
        public void Release()
        {
            switch (type)
            {
                case LockType.Monitor:
                    System.Threading.Monitor.Exit(lockedObject);
                    return;
                case LockType.ReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).ExitReadLock();
                    return;
                case LockType.WriteLock:
                    As<ReaderWriterLockSlim>(lockedObject).ExitWriteLock();
                    return;
                case LockType.UpgradableReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).ExitUpgradeableReadLock();
                    return;
                case LockType.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Release();
                    return;
            }
        }

        internal void DestroyUnderlyingLock()
        {
            if (owner)
                (lockedObject as IDisposable)?.Dispose();
        }

        void IDisposable.Dispose() => Release();

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Lock other) => type == other.type && ReferenceEquals(lockedObject, other.lockedObject);

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is Lock @lock && Equals(@lock);

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
            return hashCode;
        }

        /// <summary>
        /// Determines whether two locks are the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in Lock first, in Lock second) => ReferenceEquals(first.lockedObject, second.lockedObject) && first.type == second.type;

        /// <summary>
        /// Determines whether two locks are not the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in Lock first, in Lock second) => !ReferenceEquals(first.lockedObject, second.lockedObject) || first.type != second.type;
    }
}