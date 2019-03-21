using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides a set of methods to acquire different types
    /// of lock.
    /// </summary>
    public static class LockAcquisition
    {
        private static readonly UserDataSlot<ReaderWriterLockSlim> ReaderWriterLock = UserDataSlot<ReaderWriterLockSlim>.Allocate();
        private static readonly Func<ReaderWriterLockSlim> ReaderWriterLockFactory = () => new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

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
        /// This methods acquires monitor lock and equivalent 
        /// to <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock statement</see>.
        /// </remarks>
        public static Lock Lock<T>(this T obj)
            where T : class
            => Acquire(obj, Threading.Lock.Monitor);

        /// <summary>
        /// Acquires monitor lock.
        /// </summary>
        /// <typeparam name="T">Type of object to lock.</typeparam>
        /// <param name="obj">The object to lock.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The lock representing acquired monitor lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock Lock<T>(this T obj, TimeSpan timeout)
            where T : class
            => Acquire(obj, Threading.Lock.Monitor, timeout);

        /// <summary>
        /// Blocks the current thread until it can enter the semaphore.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <returns>The semaphore lock.</returns>
        public static Lock Lock(this SemaphoreSlim semaphore) => Acquire(semaphore, Threading.Lock.Semaphore);

        /// <summary>
        /// Blocks the current thread until it can enter the semaphore.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The semaphore lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock Lock(this SemaphoreSlim semaphore, TimeSpan timeout) => Acquire(semaphore, Threading.Lock.Semaphore, timeout);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReaderWriterLockSlim GetReaderWriterLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ReaderWriterLock, ReaderWriterLockFactory);

        /// <summary>
        /// Acquires read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired read lock.</returns>
        public static Lock ReadLock<T>(this T obj) where T : class => obj.GetReaderWriterLock().ReadLock();


        /// <summary>
        /// Acquires read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock ReadLock<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().ReadLock(timeout);
        
        /// <summary>
        /// Acquires read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired read lock.</returns>
        public static Lock ReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.ReadLock);

        /// <summary>
        /// Acquires read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock ReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.ReadLock, timeout);

        /// <summary>
        /// Acquires write lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired write lock.</returns>
        public static Lock WriteLock<T>(this T obj) where T : class => obj.GetReaderWriterLock().WriteLock();

        /// <summary>
        /// Acquires write lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock WriteLock<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().WriteLock(timeout);

        /// <summary>
        /// Acquires write lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired write lock.</returns>
        public static Lock WriteLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.WriteLock);

        /// <summary>
        /// Acquires write lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock WriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.WriteLock, timeout);

        /// <summary>
        /// Acquires upgradable read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        public static Lock UpgradableReadLock<T>(this T obj) where T : class => obj.GetReaderWriterLock().UpgradableReadLock();

        /// <summary>
        /// Acquires upgradable read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock UpgradableReadLock<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().UpgradableReadLock(timeout);

        /// <summary>
        /// Acquires upgradable read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock)
            => Acquire(rwLock, Threading.Lock.UpgradableReadLock);

        /// <summary>
        /// Acquires upgradable read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock UpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout)
            => Acquire(rwLock, Threading.Lock.UpgradableReadLock, timeout);
    }
}