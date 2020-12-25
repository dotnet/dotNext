using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncReaderWriterLockTests : Test
    {
        [Fact]
        public static async Task TrivialLock()
        {
            using var rwLock = new AsyncReaderWriterLock();
            //read lock
            True(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
            True(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitReadLock();
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitReadLock();
            //write lock
            True(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitWriteLock();
            //upgradeable read lock
            True(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
            True(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
            True(await rwLock.TryEnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(20)));
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            False(await rwLock.TryEnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitUpgradeableReadLock();
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            True(await rwLock.TryEnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitReadLock();
            False(await rwLock.TryEnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.ExitReadLock();
            rwLock.ExitUpgradeableReadLock();
        }

        [Fact]
        public static async Task InvalidExits()
        {
            using var rwLock = new AsyncReaderWriterLock();
            Throws<SynchronizationLockException>(rwLock.ExitReadLock);
            Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
            Throws<SynchronizationLockException>(rwLock.ExitWriteLock);

            await rwLock.EnterReadLockAsync(TimeSpan.FromMilliseconds(10));
            Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
            Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
            rwLock.ExitReadLock();

            await rwLock.EnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(10));
            Throws<SynchronizationLockException>(rwLock.ExitReadLock);
            Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
            rwLock.ExitUpgradeableReadLock();

            await rwLock.EnterWriteLockAsync(TimeSpan.FromMilliseconds(10));
            Throws<SynchronizationLockException>(rwLock.ExitReadLock);
            Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
            rwLock.ExitWriteLock();
        }

        [Fact]
        public static async Task WriterToWriterChain()
        {
            using var are = new AutoResetEvent(false);
            using var rwLock = new AsyncReaderWriterLock();
            True(await rwLock.TryEnterWriteLockAsync(TimeSpan.Zero));
            var task = new TaskCompletionSource<bool>();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                await rwLock.EnterWriteLockAsync(DefaultTimeout);
                rwLock.ExitWriteLock();
                task.SetResult(true);
            });
            are.WaitOne(DefaultTimeout);
            rwLock.ExitWriteLock();
            await task.Task;
        }

        [Fact]
        public static async Task WriterToReaderChain()
        {
            using var are = new AutoResetEvent(false);
            using var rwLock = new AsyncReaderWriterLock();
            await rwLock.EnterWriteLockAsync(DefaultTimeout);
            var task = new TaskCompletionSource<bool>();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(static ev => ev.Set(), are, false));
                await rwLock.EnterReadLockAsync(DefaultTimeout);
                rwLock.ExitReadLock();
                task.SetResult(true);
            });
            are.WaitOne(DefaultTimeout);
            rwLock.ExitWriteLock();
            await task.Task;
        }

        [Fact]
        public static async Task WriterToUpgradeableReaderChain()
        {
            using var are = new AutoResetEvent(false);
            using var rwLock = new AsyncReaderWriterLock();
            await rwLock.EnterWriteLockAsync(DefaultTimeout);
            var task = new TaskCompletionSource<bool>();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                False(await rwLock.TryEnterUpgradeableReadLockAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(static ev => ev.Set(), are, false));
                await rwLock.EnterUpgradeableReadLockAsync(DefaultTimeout);
                rwLock.ExitUpgradeableReadLock();
                task.SetResult(true);
            });
            are.WaitOne(DefaultTimeout);
            rwLock.ExitWriteLock();
            await task.Task;
        }

        [Fact]
        public static void OptimisticRead()
        {
            using var rwLock = new AsyncReaderWriterLock();
            var stamp = rwLock.TryOptimisticRead();
            True(stamp.IsValid);
            True(rwLock.TryEnterReadLock());
            Equal(1, rwLock.CurrentReadCount);
            True(stamp.IsValid);
            rwLock.ExitReadLock();
            Equal(stamp, rwLock.TryOptimisticRead());
            True(rwLock.TryEnterWriteLock());
            False(rwLock.IsReadLockHeld);
            True(rwLock.IsWriteLockHeld);
            False(stamp.IsValid);
        }

        [Fact]
        public static void CallDisposeTwice()
        {
            var @lock = new AsyncReaderWriterLock();
            @lock.Dispose();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void DisposeAsyncCompletedAsynchronously()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void GracefulShutdown()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterWriteLock());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            @lock.ExitWriteLock();
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(() => @lock.TryEnterReadLock());
        }

        [Fact]
        public static void GracefulShutdown2()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterReadLock());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition = @lock.EnterWriteLockAsync(CancellationToken.None);
            False(acquisition.IsCompleted);
            @lock.ExitReadLock();
            True(task.IsCompletedSuccessfully);
            True(acquisition.IsFaulted);
            Throws<ObjectDisposedException>(acquisition.GetAwaiter().GetResult);
        }

        [Fact]
        public static void GracefulShutdown3()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterWriteLock());
            var acquisition1 = @lock.EnterReadLockAsync(CancellationToken.None);
            False(acquisition1.IsCompleted);
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition2 = @lock.EnterReadLockAsync(CancellationToken.None);
            False(task.IsCompleted);

            @lock.ExitWriteLock();
            True(acquisition1.IsCompletedSuccessfully);
            False(acquisition2.IsCompleted);
            False(task.IsCompleted);

            @lock.ExitReadLock();
            True(acquisition2.IsFaulted);
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(acquisition2.GetAwaiter().GetResult);
        }

        [Fact]
        public static void GracefulShutdown4()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterWriteLock());
            var acquisition1 = @lock.EnterUpgradeableReadLockAsync(CancellationToken.None);
            False(acquisition1.IsCompleted);
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            var acquisition2 = @lock.EnterReadLockAsync(CancellationToken.None);
            False(task.IsCompleted);

            @lock.ExitWriteLock();
            True(acquisition1.IsCompletedSuccessfully);
            False(acquisition2.IsCompleted);
            False(task.IsCompleted);

            @lock.ExitUpgradeableReadLock();
            True(acquisition2.IsFaulted);
            True(task.IsCompletedSuccessfully);
            Throws<ObjectDisposedException>(acquisition2.GetAwaiter().GetResult);
        }
    }
}
