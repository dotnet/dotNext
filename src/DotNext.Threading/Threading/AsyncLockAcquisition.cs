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
        private static readonly UserDataSlot<AsyncLock> ExclusiveLock = UserDataSlot<AsyncLock>.Allocate();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncReaderWriterLock GetReaderWriterLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ReaderWriterLock, () => new AsyncReaderWriterLock());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncLock GetExclusiveLock<T>(this T obj)
            where T : class
            => obj.GetUserData().GetOrSet(ExclusiveLock, AsyncLock.Exclusive);

        /// <summary>
        /// Blocks the current thread until it can enter the semaphore.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <param name="token">The token that can be used to cancel lock acquisition.</param>
        /// <returns>The acquired semaphore lock.</returns>
        public static Task<AsyncLock.Holder> AcquireLock(this SemaphoreSlim semaphore, CancellationToken token) => AsyncLock.Semaphore(semaphore).Acquire(token);

        /// <summary>
        /// Blocks the current thread until it can enter the semaphore.
        /// </summary>
        /// <param name="semaphore">The semaphore.</param>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The acquired semaphore lock.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireLock(this SemaphoreSlim semaphore, TimeSpan timeout) => AsyncLock.Semaphore(semaphore).Acquire(timeout);

        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetExclusiveLock().Acquire(timeout);

        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetExclusiveLock().Acquire(token);

        public static Task<AsyncLock.Holder> AcquireReadLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, false).Acquire(timeout);

        public static Task<AsyncLock.Holder> AcquireReadLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, false).Acquire(token);

        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireReadLockAsync(timeout);

        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireReadLockAsync(token);

        public static Task<AsyncLock.Holder> AcquireWriteLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.WriteLock(rwLock).Acquire(timeout);

        public static Task<AsyncLock.Holder> AcquireWriteLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.WriteLock(rwLock).Acquire(token);

        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireWriteLockAsync(timeout);

        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireWriteLockAsync(token);

        public static Task<AsyncLock.Holder> AcquireUpgradableReadLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, true).Acquire(timeout);

        public static Task<AsyncLock.Holder> AcquireUpgradableReadLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, true).Acquire(token);

        public static Task<AsyncLock.Holder> AcquireUpgradableReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().AcquireUpgradableReadLockAsync(timeout);

        public static Task<AsyncLock.Holder> AcquireUpgradableReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().AcquireUpgradableReadLockAsync(token);
    }
}