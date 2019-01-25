using System;
using System.Threading;

namespace DotNext.Threading
{
    public static class LockHelper
    {
        private static Lock Acquire<T>(T obj, Converter<T, Lock> locker)
            where T : class
        {
            var result = locker(obj);
            result.Acquire();
            return result;
        }

        private static Lock Acquire<T>(T obj, Converter<T, Lock> locker, TimeSpan timeout)
            where T : class
        {
            var result = locker(obj);
            return result.TryAcquire(timeout) ? result : throw new TimeoutException();
        }

        public static Lock WriteLock<T>(this T obj)
            where T : class
            => Acquire(obj, Lock.Monitor);

        public static Lock MonitorEnter<T>(this T obj, TimeSpan timeout)
            where T : class
            => Acquire(obj, Lock.Monitor, timeout);

        public static Lock ReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Lock.ReadLock);

        public static Lock ReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Lock.ReadLock, timeout);

        public static Lock WriteLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Lock.WriteLock);

        public static Lock WriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Lock.WriteLock, timeout);

        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Lock.UpgradableReadLock);

        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Lock.UpgradableReadLock, timeout);
    }
}