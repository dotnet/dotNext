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

        /// <summary>
        /// Acquires exclusive lock for the specified object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">An object to be locked.</param>
        /// <returns>Lock control object.</returns>
        /// <remarks>
        /// This methods acquires monitor lock.
        /// </remarks>
        public static Lock Lock<T>(this T obj)
            where T : class
            => Acquire(obj, Threading.Lock.Monitor);

        public static Lock Lock<T>(this T obj, TimeSpan timeout)
            where T : class
            => Acquire(obj, Threading.Lock.Monitor, timeout);

        public static Lock ReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.ReadLock);

        public static Lock ReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.ReadLock, timeout);

        public static Lock WriteLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.WriteLock);

        public static Lock WriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.WriteLock, timeout);

        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.UpgradableReadLock);

        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.UpgradableReadLock, timeout);
    }
}