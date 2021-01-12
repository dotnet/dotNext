using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncReaderWriterLockTests : Assert
    {
        [Fact]
        public static async Task TrivialLock()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
                //read lock
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                //write lock
                True(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                False(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitWriteLock();
                //upgradeable read lock
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                True(await rwLock.TryEnterUpgradeableReadLock(TimeSpan.FromMilliseconds(20)));
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                False(await rwLock.TryEnterUpgradeableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitUpgradeableReadLock();
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                True(await rwLock.TryEnterUpgradeableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                False(await rwLock.TryEnterUpgradeableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                rwLock.ExitUpgradeableReadLock();
            }
        }

        [Fact]
        public static async Task InvalidExits()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);

                await rwLock.EnterReadLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
                rwLock.ExitReadLock();

                await rwLock.EnterUpgradeableReadLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
                rwLock.ExitUpgradeableReadLock();

                await rwLock.EnterWriteLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitUpgradeableReadLock);
                rwLock.ExitWriteLock();
            }
        }

        [Fact]
        public static async Task WriterToWriterChain()
        {
            using (var are = new AutoResetEvent(false))
            using (var rwLock = new AsyncReaderWriterLock())
            {
                True(await rwLock.TryEnterWriteLock(TimeSpan.Zero));
                var task = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async state =>
                {
                    False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await rwLock.EnterWriteLock(InfiniteTimeSpan);
                    rwLock.ExitWriteLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }

        [Fact]
        public static async Task WriterToReaderChain()
        {
            using (var are = new AutoResetEvent(false))
            using (var rwLock = new AsyncReaderWriterLock())
            {
                await rwLock.EnterWriteLock(InfiniteTimeSpan);
                var task = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async state =>
                {
                    False(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await rwLock.EnterReadLock(InfiniteTimeSpan);
                    rwLock.ExitReadLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }

        [Fact]
        public static async Task WriterToUpgradeableReaderChain()
        {
            using (var are = new AutoResetEvent(false))
            using (var rwLock = new AsyncReaderWriterLock())
            {
                await rwLock.EnterWriteLock(InfiniteTimeSpan);
                var task = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async state =>
                {
                    False(await rwLock.TryEnterUpgradeableReadLock(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await rwLock.EnterUpgradeableReadLock(InfiniteTimeSpan);
                    rwLock.ExitUpgradeableReadLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }

        [Fact]
        public static void OptimisticRead()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
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
        }
    }
}
