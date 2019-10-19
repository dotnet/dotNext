namespace DotNext.Threading
{
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
    }
}