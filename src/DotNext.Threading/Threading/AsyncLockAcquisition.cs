using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides a set of methods to acquire different types of asynchronous lock.
    /// </summary>
    public static class AsyncLockAcquisition
    {
        private static readonly UserDataSlot<AsyncReaderWriterLock> ReaderWriterLock = UserDataSlot<AsyncReaderWriterLock>.Allocate();
        private static readonly UserDataSlot<AsyncExclusiveLock> ExclusiveLock = UserDataSlot<AsyncExclusiveLock>.Allocate();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncReaderWriterLock GetReaderWriterLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ReaderWriterLock, () => new AsyncReaderWriterLock());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncExclusiveLock GetExclusiveLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ExclusiveLock, () => new AsyncExclusiveLock());

        /// <summary>
        /// Acquires semaphore lock asynchronously.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <param name="token">The token that can be used to cancel lock acquisition.</param>
        /// <returns>The acquired semaphore lock.</returns>
        public static Task<AsyncLock.Holder> AcquireLockAsync(this SemaphoreSlim semaphore, CancellationToken token) => AsyncLock.Semaphore(semaphore).Acquire(token);

        /// <summary>
        /// Acquires semaphore lock asynchronously.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired semaphore lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireLockAsync(this SemaphoreSlim semaphore, TimeSpan timeout) => AsyncLock.Semaphore(semaphore).Acquire(timeout);

        /// <summary>
        /// Acquires exclusive lock asynchronously.
        /// </summary>
        /// <param name="lock">The exclusive lock to acquire.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireLock(this AsyncExclusiveLock @lock, CancellationToken token) => AsyncLock.Exclusive(@lock).Acquire(token);

        /// <summary>
        /// Acquires exclusive lock asynchronously.
        /// </summary>
        /// <param name="lock">The exclusive lock to acquire.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireLock(this AsyncExclusiveLock @lock, TimeSpan timeout) => AsyncLock.Exclusive(@lock).Acquire(timeout);

        /// <summary>
        /// Acquires exclusive lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetExclusiveLock().AcquireLock(timeout);

        /// <summary>
        /// Acquires exclusive lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetExclusiveLock().AcquireLock(token);

        /// <summary>
        /// Acquires reader lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireReadLock(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, false).Acquire(timeout);

        /// <summary>
        /// Acquires reader lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireReadLock(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, false).Acquire(token);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireReadLock(timeout);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireReadLock(token);

        /// <summary>
        /// Acquires writer lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireWriteLock(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.WriteLock(rwLock).Acquire(timeout);

        /// <summary>
        /// Acquires writer lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireWriteLock(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.WriteLock(rwLock).Acquire(token);

        /// <summary>
        /// Acquires writer lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireWriteLock(timeout);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireWriteLock(token);

        /// <summary>
        /// Acquires upgradeable lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLock(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, true).Acquire(timeout);

        /// <summary>
        /// Acquires upgradeable lock.
        /// </summary>
        /// <param name="rwLock">Read/write lock provider.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLock(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, true).Acquire(token);

        /// <summary>
        /// Acquires upgradeable lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireUpgradeableReadLock(timeout);

        /// <summary>
        /// Acquires upgradeable lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireUpgradeableReadLock(token);
    }
}