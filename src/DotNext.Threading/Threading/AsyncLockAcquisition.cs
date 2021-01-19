using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
        {
            switch (obj)
            {
                case null:
                    throw new ArgumentNullException(nameof(obj));
                case AsyncReaderWriterLock rwl:
                    return rwl;
                case AsyncSharedLock or ReaderWriterLockSlim or AsyncExclusiveLock or SemaphoreSlim or WaitHandle or ReaderWriterLock _:
                case string str when string.IsInterned(str) is not null:
                    throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
                default:
                    return obj.GetUserData().GetOrSet(ReaderWriterLock);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncLock GetExclusiveLock<T>(this T obj)
            where T : class
        {
            AsyncLock @lock;
            switch (obj)
            {
                case null:
                    throw new ArgumentNullException(nameof(obj));
                case AsyncSharedLock shared:
                    @lock = AsyncLock.Exclusive(shared);
                    break;
                case AsyncExclusiveLock exclusive:
                    @lock = AsyncLock.Exclusive(exclusive);
                    break;
                case SemaphoreSlim semaphore:
                    @lock = AsyncLock.Semaphore(semaphore);
                    break;
                case AsyncReaderWriterLock rwl:
                    @lock = AsyncLock.WriteLock(rwl);
                    break;
                case ReaderWriterLockSlim or WaitHandle or ReaderWriterLock _:
                case string str when string.IsInterned(str) is not null:
                    throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
                default:
                    @lock = AsyncLock.Exclusive(obj.GetUserData().GetOrSet(ExclusiveLock));
                    break;
            }

            return @lock;
        }

        /// <summary>
        /// Acquires exclusive lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, TimeSpan timeout)
            where T : class => obj.GetExclusiveLock().AcquireAsync(timeout);

        /// <summary>
        /// Acquires exclusive lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, CancellationToken token)
            where T : class => obj.GetExclusiveLock().AcquireAsync(token);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, TimeSpan timeout)
            where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock(), false).AcquireAsync(timeout);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, CancellationToken token)
            where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock(), false).AcquireAsync(token);

        /// <summary>
        /// Acquires writer lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, TimeSpan timeout)
            where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(timeout);

        /// <summary>
        /// Acquires reader lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, CancellationToken token)
            where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(token);

        /// <summary>
        /// Acquires upgradeable lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The acquired lock holder.</returns>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLockAsync<T>(this T obj, TimeSpan timeout)
            where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock(), true).AcquireAsync(timeout);

        /// <summary>
        /// Acquires upgradeable lock associated with the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object to be locked.</typeparam>
        /// <param name="obj">The object to be locked.</param>
        /// <param name="token">The token that can be used to abort acquisition operation.</param>
        /// <returns>The acquired lock holder.</returns>
        public static Task<AsyncLock.Holder> AcquireUpgradeableReadLockAsync<T>(this T obj, CancellationToken token)
            where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock(), true).AcquireAsync(token);
    }
}