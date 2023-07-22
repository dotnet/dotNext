using System.Runtime.CompilerServices;

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
        rwLock.EnterReadLock();
        rwLock.EnterReadLock();
        Equal(2, rwLock.CurrentReadCount);
        False(rwLock.TryEnterWriteLock(TimeSpan.Zero));
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
    public static void OptimisticRead()
    {
        var rwLock = new ReaderWriterSpinLock();
        var stamp = rwLock.TryOptimisticRead();
        True(rwLock.Validate(in stamp));
        True(rwLock.TryEnterReadLock());
        Equal(1, rwLock.CurrentReadCount);
        True(rwLock.Validate(stamp));
        rwLock.ExitReadLock();
        Equal(stamp, rwLock.TryOptimisticRead());
        True(rwLock.TryEnterWriteLock());
        False(rwLock.IsReadLockHeld);
        True(rwLock.IsWriteLockHeld);
        False(rwLock.Validate(stamp));
        False(rwLock.TryEnterReadLock(TimeSpan.Zero));
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
            False(rwLock.Value.TryEnterWriteLock(TimeSpan.FromMilliseconds(10)));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            True(rwLock.Value.TryEnterWriteLock(DefaultTimeout));
            rwLock.Value.ExitWriteLock();
        }, rwLock);

        await are.Task.WaitAsync(DefaultTimeout);
        rwLock.Value.ExitWriteLock();
        await task.WaitAsync(DefaultTimeout);
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
            False(rwLock.Value.TryEnterWriteLock(TimeSpan.FromMilliseconds(10)));
            True(ThreadPool.QueueUserWorkItem(static ev => ev.SetResult(), are, false));
            True(rwLock.Value.TryEnterReadLock(DefaultTimeout));
            rwLock.Value.ExitReadLock();
        }, rwLock);

        await are.Task.WaitAsync(DefaultTimeout);
        rwLock.Value.ExitWriteLock();
        await task.WaitAsync(DefaultTimeout);
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