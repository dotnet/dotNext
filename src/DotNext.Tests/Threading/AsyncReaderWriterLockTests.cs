using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncReaderWriterLockTests : Test
    {
        [Fact]
        public static async Task TrivialLock()
        {
            using var rwLock = new AsyncReaderWriterLock(3);

            // read lock
            True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
            True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.Release();
            False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.Release();

            // write lock
            True(await rwLock.TryEnterWriteLockAsync(DefaultTimeout));
            False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(20)));
            rwLock.Release();

            // upgrade to write lock
            True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
            True(await rwLock.TryUpgradeToWriteLockAsync(DefaultTimeout));
            False(rwLock.TryEnterWriteLock());
            rwLock.DowngradeFromWriteLock();
            True(await rwLock.TryEnterReadLockAsync(DefaultTimeout));
        }

        [Fact]
        public static async Task WriterToWriterChain()
        {
            var are = new TaskCompletionSource();
            using var rwLock = new AsyncReaderWriterLock();
            True(await rwLock.TryEnterWriteLockAsync(TimeSpan.Zero));
            var task = Task.Run(async () =>
            {
                False(await rwLock.TryEnterWriteLockAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(ev => ev.SetResult(), are, false));
                await rwLock.EnterWriteLockAsync(DefaultTimeout);
                rwLock.Release();
                return true;
            });

            await are.Task.WaitAsync(DefaultTimeout);
            rwLock.Release();
            True(await task);
        }

        [Fact]
        public static async Task WriterToReaderChain()
        {
            var are = new TaskCompletionSource();
            using var rwLock = new AsyncReaderWriterLock();
            await rwLock.EnterWriteLockAsync(DefaultTimeout);
            var task = Task.Run(async () =>
            {
                False(await rwLock.TryEnterReadLockAsync(TimeSpan.FromMilliseconds(10)));
                True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
                await rwLock.EnterReadLockAsync(DefaultTimeout);
                rwLock.Release();
                return true;
            });

            await are.Task.WaitAsync(DefaultTimeout);
            rwLock.Release();
            True(await task);
        }

        [Fact]
        public static void OptimisticRead()
        {
            using var rwLock = new AsyncReaderWriterLock();
            var stamp = rwLock.TryOptimisticRead();
            True(rwLock.Validate(stamp));
            True(rwLock.TryEnterReadLock());
            Equal(1, rwLock.CurrentReadCount);
            True(rwLock.Validate(stamp));
            rwLock.Release();
            Equal(stamp, rwLock.TryOptimisticRead());
            True(rwLock.TryEnterWriteLock());
            False(rwLock.IsReadLockHeld);
            True(rwLock.IsWriteLockHeld);
            False(rwLock.Validate(stamp));
        }

        [Fact]
        public static void CallDisposeTwice()
        {
            var @lock = new AsyncReaderWriterLock();
            @lock.Dispose();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void DisposeAsyncCompletedSynchronously()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task GracefulShutdown()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterWriteLock());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            @lock.Release();
            await task;
            Throws<ObjectDisposedException>(() => @lock.TryEnterReadLock());
        }

        [Fact]
        public static async Task GracefulShutdown2()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterReadLock());
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);
            await ThrowsAsync<ObjectDisposedException>(@lock.EnterWriteLockAsync().AsTask);
            @lock.Release();
            await task;
        }

        [Fact]
        public static async Task GracefulShutdown3()
        {
            using var @lock = new AsyncReaderWriterLock();
            True(@lock.TryEnterWriteLock());
            var acquisition1 = @lock.EnterReadLockAsync();
            False(acquisition1.IsCompleted);
            var task = @lock.DisposeAsync();
            False(task.IsCompleted);

            await ThrowsAsync<ObjectDisposedException>(@lock.EnterReadLockAsync().AsTask);

            @lock.Release();
            await acquisition1;
            False(task.IsCompleted);

            @lock.Release();
            await task;
        }
    }
}
