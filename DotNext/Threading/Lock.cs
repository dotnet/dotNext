using System;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents unified representation monitor lock, read lock,
    /// write lock or upgradable read lock.
    /// </summary>
    public readonly struct Lock : IDisposable
    {
        private enum LockType: byte
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

        public static Lock Monitor(object obj)
            => new Lock(obj ?? throw new ArgumentNullException(nameof(obj)), LockType.Monitor);

        public static Lock ReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.ReadLock);

        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.WriteLock);

        public static Lock UpgradableReadLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), LockType.UpgradableReadLock);

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

        public bool TryAcquire()
        {
            switch(type)
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
            switch(type)
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

        public void Release()
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

        void IDisposable.Dispose() => Release();
    }
}
