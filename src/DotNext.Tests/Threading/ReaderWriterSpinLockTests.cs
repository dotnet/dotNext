using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

public sealed class ReaderWriterSpinLockTests : Test
{
    [Fact]
    public static void BasicChecks()
    {
        var rwLock = new ReaderWriterSpinLock();
        False(rwLock.IsReadLockHeld);
        False(rwLock.IsWriteLockHeld);
        Equal(0, rwLock.CurrentReadCount);
        True(rwLock.TryEnterReadLock());
        True(rwLock.TryEnterReadLock());
        Equal(2, rwLock.CurrentReadCount);
        False(rwLock.TryEnterWriteLock(TimeSpan.Zero, TestToken));
        True(rwLock.IsReadLockHeld);
        False(rwLock.IsWriteLockHeld);
        rwLock.ExitReadLock();
        rwLock.ExitReadLock();
        rwLock.EnterWriteLock();
        Equal(0, rwLock.CurrentReadCount);
        True(rwLock.IsWriteLockHeld);
        False(rwLock.IsReadLockHeld);
    }

    [Fact]
    public static async Task WriterToWriterChain()
    {
        var are = new TaskCompletionSource();
        var rwLock = new StrongBox<ReaderWriterSpinLock>();
        True(rwLock.Value.TryEnterWriteLock());
        var task = Task.Factory.StartNew(state =>
        {
            var rwLock = (StrongBox<ReaderWriterSpinLock>)state;
            False(rwLock.Value.TryEnterWriteLock(TimeSpan.FromMilliseconds(10), TestToken));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            True(rwLock.Value.TryEnterWriteLock(InfiniteTimeSpan, TestToken));
            rwLock.Value.ExitWriteLock();
        }, rwLock, TestToken);

        await are.Task.WaitAsync(TestToken);
        rwLock.Value.ExitWriteLock();
        await task.WaitAsync(TestToken);
    }

    [Fact]
    public static async Task WriterToReaderChain()
    {
        var are = new TaskCompletionSource();
        var rwLock = new StrongBox<ReaderWriterSpinLock>();
        True(rwLock.Value.TryEnterWriteLock());
        var task = Task.Factory.StartNew(state =>
        {
            var rwLock = (StrongBox<ReaderWriterSpinLock>)state;
            False(rwLock.Value.TryEnterWriteLock(TimeSpan.FromMilliseconds(10), TestToken));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            True(rwLock.Value.TryEnterReadLock(InfiniteTimeSpan, TestToken));
            rwLock.Value.ExitReadLock();
        }, rwLock, TestToken);

        await are.Task.WaitAsync(TestToken);
        rwLock.Value.ExitWriteLock();
        await task.WaitAsync(TestToken);
    }

    [Fact]
    public static void ReadLockUpgrade1()
    {
        var rwLock = new ReaderWriterSpinLock();
        rwLock.EnterReadLock();
        False(rwLock.IsWriteLockHeld);

        True(rwLock.TryUpgradeToWriteLock());
        True(rwLock.IsWriteLockHeld);

        rwLock.DowngradeFromWriteLock();
        False(rwLock.IsWriteLockHeld);
        True(rwLock.IsReadLockHeld);
    }

    [Fact]
    public static void ReadLockUpgrade2()
    {
        var rwLock = new ReaderWriterSpinLock();
        rwLock.EnterReadLock();
        False(rwLock.IsWriteLockHeld);

        rwLock.UpgradeToWriteLock();
        True(rwLock.IsWriteLockHeld);

        rwLock.DowngradeFromWriteLock();
        False(rwLock.IsWriteLockHeld);
        True(rwLock.IsReadLockHeld);
    }

    [Fact]
    public static void ReadLockUpgrade3()
    {
        var rwLock = new ReaderWriterSpinLock();
        rwLock.EnterReadLock();
        False(rwLock.IsWriteLockHeld);

        True(rwLock.TryUpgradeToWriteLock(TimeSpan.Zero, CancellationToken.None));
        True(rwLock.IsWriteLockHeld);

        rwLock.DowngradeFromWriteLock();
        False(rwLock.IsWriteLockHeld);
        True(rwLock.IsReadLockHeld);
    }
}