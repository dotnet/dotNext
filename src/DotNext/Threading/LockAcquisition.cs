using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides a set of methods to acquire different types of lock.
    /// </summary>
    public static class LockAcquisition
    {
        private static readonly UserDataSlot<ReaderWriterLockSlim> ReaderWriterLock = UserDataSlot<ReaderWriterLockSlim>.Allocate();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReaderWriterLockSlim GetReaderWriterLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ReaderWriterLock, () => new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));

        /// <summary>
        /// Acquires read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired read lock.</returns>
        public static Lock.Holder ReadLock(this ReaderWriterLockSlim rwLock) => Lock.ReadLock(rwLock, false).Acquire();

        /// <summary>
        /// Acquires read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder ReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout) => Lock.ReadLock(rwLock, false).Acquire(timeout);

        /// <summary>
        /// Acquires read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired read lock.</returns>
        public static Lock.Holder ReadLock<T>(this T obj) where T : class => obj.GetReaderWriterLock().ReadLock();

        /// <summary>
        /// Acquires read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder ReadLock<T>(this T obj, TimeSpan timeout) where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), false).Acquire(timeout);

        /// <summary>
        /// Acquires write lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired write lock.</returns>
        public static Lock.Holder WriteLock(this ReaderWriterLockSlim rwLock) => Lock.WriteLock(rwLock).Acquire();

        /// <summary>
        /// Acquires write lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder WriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout) => Lock.WriteLock(rwLock).Acquire(timeout);

        /// <summary>
        /// Acquires write lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired write lock.</returns>
        public static Lock.Holder WriteLock<T>(this T obj) where T : class => Lock.WriteLock(obj.GetReaderWriterLock()).Acquire();

        /// <summary>
        /// Acquires write lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder WriteLock<T>(this T obj, TimeSpan timeout) where T : class => Lock.WriteLock(obj.GetReaderWriterLock()).Acquire(timeout);

        /// <summary>
        /// Acquires upgradable read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        public static Lock.Holder UpgradableReadLock(this ReaderWriterLockSlim rwLock) => Lock.ReadLock(rwLock, true).Acquire();

        /// <summary>
        /// Acquires upgradable read lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder UpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeout) => Lock.ReadLock(rwLock, true).Acquire(timeout);

        /// <summary>
        /// Acquires upgradable read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        public static Lock.Holder UpgradableReadLock<T>(this T obj) where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), true).Acquire();

        /// <summary>
        /// Acquires upgradable read lock for the specified object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired upgradable read lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Lock.Holder UpgradableReadLock<T>(this T obj, TimeSpan timeout) where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), false).Acquire();
    }
}