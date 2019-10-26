using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class ReaderWriterSpinLockTests : Assert
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
    }
}