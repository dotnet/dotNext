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

        public static Task<AsyncLock.Holder> LockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetExclusiveLock().Acquire(timeout);

        public static Task<AsyncLock.Holder> LockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetExclusiveLock().Acquire(token);

        public static Task<AsyncLock.Holder> ReadLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, false).Acquire(timeout);

        public static Task<AsyncLock.Holder> ReadLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, false).Acquire(token);

        public static Task<AsyncLock.Holder> ReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().ReadLockAsync(timeout);

        public static Task<AsyncLock.Holder> ReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().ReadLockAsync(token);

        public static Task<AsyncLock.Holder> WriteLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.WriteLock(rwLock).Acquire(timeout);

        public static Task<AsyncLock.Holder> WriteLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.WriteLock(rwLock).Acquire(token);

        public static Task<AsyncLock.Holder> WriteLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().WriteLockAsync(timeout);

        public static Task<AsyncLock.Holder> WriteLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().WriteLockAsync(token);

        public static Task<AsyncLock.Holder> UpgradableReadLockAsync(this AsyncReaderWriterLock rwLock, TimeSpan timeout) => AsyncLock.ReadLock(rwLock, true).Acquire(timeout);

        public static Task<AsyncLock.Holder> UpgradableReadLockAsync(this AsyncReaderWriterLock rwLock, CancellationToken token) => AsyncLock.ReadLock(rwLock, true).Acquire(token);

        public static Task<AsyncLock.Holder> UpgradableReadLockAsync<T>(this T obj, TimeSpan timeout) where T : class => obj.GetReaderWriterLock().UpgradableReadLockAsync(timeout);

        public static Task<AsyncLock.Holder> UpgradableReadLockAsync<T>(this T obj, CancellationToken token) where T : class => obj.GetReaderWriterLock().UpgradableReadLockAsync(token);
    }
}