using System;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents unified representation monitor lock, read lock,
    /// write lock or upgradable read lock.
    /// </summary>
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

        private Lock(object obj, LockType type)
        {
            lockedObject = obj;
            this.type = type;
        }

        /// <summary>
        /// Creates semaphore-based lock but doesn't acquire lock.
        /// </summary>
        /// <param name="semaphore">The semaphore to wrap into lock object.</param>
        /// <returns>The lock representing semaphore.</returns>
        public static Lock Semaphore(SemaphoreSlim semaphore)
            => new Lock(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), LockType.Semaphore);

        /// <summary>
        /// Creates monitor-based lock control object but doesn't acquire lock.
        /// </summary>
        /// <param name="obj">Monitor lock target.</param>
        /// <returns>The lock representing monitor.</returns>
        public static Lock Monitor(object obj)
            => new Lock(obj ?? throw new ArgumentNullException(nameof(obj)), LockType.Monitor);

        /// <summary>
        /// Creates exclusive lock.
        /// </summary>
        /// <returns>The exclusive lock.</returns>
        public static Lock Monitor() => Monitor(new object());

        /// <summary>
        /// Creates read lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Read-only lock.</returns>
        public static Lock ReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.ReadLock);

        /// <summary>
        /// Creates write lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Write-only lock.</returns>
        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.WriteLock);

        /// <summary>
        /// Creates upgradable read lock but doesn't acquire.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Upgradable read lock.</returns>
        public static Lock UpgradableReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.UpgradableReadLock);

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
                    ((ReaderWriterLockSlim)lockedObject).EnterReadLock();
                    return;
                case LockType.WriteLock:
                    ((ReaderWriterLockSlim)lockedObject).EnterWriteLock();
                    return;
                case LockType.UpgradableReadLock:
                    ((ReaderWriterLockSlim)lockedObject).EnterUpgradeableReadLock();
                    return;
                case LockType.Semaphore:
                    ((SemaphoreSlim)lockedObject).Wait();
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
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterReadLock(0);
                case LockType.WriteLock:
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterWriteLock(0);
                case LockType.UpgradableReadLock:
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterUpgradeableReadLock(0);
                case LockType.Semaphore:
                    return ((SemaphoreSlim)lockedObject).Wait(0);
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
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterReadLock(timeout);
                case LockType.WriteLock:
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterWriteLock(timeout);
                case LockType.UpgradableReadLock:
                    return ((ReaderWriterLockSlim)lockedObject).TryEnterUpgradeableReadLock(timeout);
                case LockType.Semaphore:
                    return ((SemaphoreSlim)lockedObject).Wait(timeout);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Releases acquired lock.
        /// </summary>
        public void Dispose()
        {
            switch (type)
            {
                case LockType.Monitor:
                    System.Threading.Monitor.Exit(lockedObject);
                    return;
                case LockType.ReadLock:
                    ((ReaderWriterLockSlim)lockedObject).ExitReadLock();
                    return;
                case LockType.WriteLock:
                    ((ReaderWriterLockSlim)lockedObject).ExitWriteLock();
                    return;
                case LockType.UpgradableReadLock:
                    ((ReaderWriterLockSlim)lockedObject).ExitUpgradeableReadLock();
                    return;
                case LockType.Semaphore:
                    ((SemaphoreSlim)lockedObject).Release();
                    return;
            }
        }

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in Lock other) => type == other.type && Equals(lockedObject, other.lockedObject);

        bool IEquatable<Lock>.Equals(Lock other) => Equals(in other);

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
        public static bool operator ==(in Lock first, in Lock second) => first.Equals(second);

        /// <summary>
        /// Determines whether two locks are not the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in Lock first, in Lock second) => !first.Equals(second);
    }
}