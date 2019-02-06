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
            WriteLock
        }

        private readonly object lockedObject;
        private readonly LockType type;

        private Lock(object obj, LockType type)
        {
            lockedObject = obj;
            this.type = type;
        }

        /// <summary>
        /// Creates monitor lock control object but doesn't acquire lock.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Lock Monitor(object obj)
            => new Lock(obj ?? throw new ArgumentNullException(nameof(obj)), LockType.Monitor);

        public static Lock Monitor() => Monitor(new object());

        public static Lock ReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.ReadLock);

        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.WriteLock);

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
                default:
                    return false;
            }
        }

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
            }
        }

        public bool Equals(in Lock other) => type == other.type && Equals(lockedObject, other.lockedObject);
        bool IEquatable<Lock>.Equals(Lock other) => Equals(in other);
        public override bool Equals(object other) => other is Lock @lock && Equals(@lock);

        public override int GetHashCode()
        {
            if (lockedObject is null)
                return 0;
            var hashCode = -549183179;
            hashCode = hashCode * -1521134295 + lockedObject.GetHashCode();
            hashCode = hashCode * -1521134295 + type.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(in Lock first, in Lock second) => first.Equals(second);
        public static bool operator !=(in Lock first, in Lock second) => !first.Equals(second);
    }
}