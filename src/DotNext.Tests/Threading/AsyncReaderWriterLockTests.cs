using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncReaderWriterLockTests : Assert
    {
        [Fact]
        public async Task TrivialLock()
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
                //upgradable read lock
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                True(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                True(await rwLock.TryEnterUpgradableReadLock(TimeSpan.FromMilliseconds(20)));
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                False(await rwLock.TryEnterUpgradableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitUpgradableReadLock();
                False(await rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(20)));
                True(await rwLock.TryEnterUpgradableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                False(await rwLock.TryEnterUpgradableReadLock(TimeSpan.FromMilliseconds(20)));
                rwLock.ExitReadLock();
                rwLock.ExitUpgradableReadLock();
            }
        }

        [Fact]
        public async Task InvalidExits()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitUpgradableReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);

                await rwLock.EnterReadLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitUpgradableReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
                rwLock.ExitReadLock();

                await rwLock.EnterUpgradableReadLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitWriteLock);
                rwLock.ExitUpgradableReadLock();

                await rwLock.EnterWriteLock(TimeSpan.FromMilliseconds(10));
                Throws<SynchronizationLockException>(rwLock.ExitReadLock);
                Throws<SynchronizationLockException>(rwLock.ExitUpgradableReadLock);
                rwLock.ExitWriteLock();
            }
        }

        [Fact]
        public async Task WriterToWriterChain()
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
                    await rwLock.EnterWriteLock(TimeSpan.MaxValue);
                    rwLock.ExitWriteLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }

        [Fact]
        public async Task WriterToReaderChain()
        {
            using (var are = new AutoResetEvent(false))
            using (var rwLock = new AsyncReaderWriterLock())
            {
                await rwLock.EnterWriteLock(TimeSpan.MaxValue);
                var task = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async state =>
                {
                    False(await rwLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await rwLock.EnterReadLock(TimeSpan.MaxValue);
                    rwLock.ExitReadLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }

        [Fact]
        public async Task WriterToUpgradeableReaderChain()
        {
            using (var are = new AutoResetEvent(false))
            using (var rwLock = new AsyncReaderWriterLock())
            {
                await rwLock.EnterWriteLock(TimeSpan.MaxValue);
                var task = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async state =>
                {
                    False(await rwLock.TryEnterUpgradableReadLock(TimeSpan.FromMilliseconds(10)));
                    True(ThreadPool.QueueUserWorkItem(ev => ev.Set(), are, false));
                    await rwLock.EnterUpgradableReadLock(TimeSpan.MaxValue);
                    rwLock.ExitUpgradableReadLock();
                    task.SetResult(true);
                });
                are.WaitOne();
                rwLock.ExitWriteLock();
                await task.Task;
            }
        }
    }
}
