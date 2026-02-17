namespace DotNext.Threading;

public sealed class QueuedSynchronizerTests : Test
{
    [Fact]
    public static async Task ThrowOnAcquisitionAsync()
    {
        await using var synchronizer = new MySynchronizer();
        await ThrowsAsync<ArithmeticException>(synchronizer.ThrowAsync(TestToken).AsTask);
        False(synchronizer.TryAcquire());
    }
    
    private sealed class MySynchronizer : QueuedSynchronizer<bool>
    {
        public ValueTask ThrowAsync(CancellationToken token = default)
            => AcquireAsync(context: false, token);

        public bool TryAcquire() => TryAcquire(context: false);

        protected override bool CanAcquire(bool context) => context;

        protected override ExceptionFactory GetAcquisitionException(bool canAcquire)
            => canAcquire ? null : ExceptionFactory.Of<ArithmeticException>();
    }

    [Fact]
    public static async Task ResumeReadLockAsync()
    {
        using var synchronizer = new CustomReaderWriterLock();
        await synchronizer.EnterReadLockAsync(TestToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        var writeLockTask = synchronizer.EnterWriteLockAsync(cts.Token).AsTask();
        False(writeLockTask.IsCompleted);

        var readLockTask = synchronizer.EnterReadLockAsync(TestToken).AsTask();

        await cts.CancelAsync();
        await readLockTask;
    }
    
    private sealed class CustomReaderWriterLock : QueuedSynchronizer<bool>
    {
        private uint readLocks;
        private bool writeLockTaken;

        protected override bool CanAcquire(bool writeLock)
        {
            if (writeLockTaken)
                return false;

            return !writeLock || readLocks is 0U;
        }

        public ValueTask EnterWriteLockAsync(CancellationToken token)
            => AcquireAsync(true, token);

        public ValueTask EnterReadLockAsync(CancellationToken token)
            => AcquireAsync(false, token);

        protected override void AcquireCore(bool writeLock)
        {
            if (writeLock)
            {
                writeLockTaken = true;
            }
            else
            {
                readLocks++;
            }
        }

        protected override void ReleaseCore(bool writeLock)
        {
            if (writeLock)
            {
                writeLockTaken = false;
            }
            else
            {
                readLocks--;
            }
        }
    }
}