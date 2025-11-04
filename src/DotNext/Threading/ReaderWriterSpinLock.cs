using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents lightweight reader-writer lock based on spin loop.
/// </summary>
/// <remarks>
/// This type should not be used to synchronize access to the I/O intensive resources.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public partial struct ReaderWriterSpinLock
{
    private const int WriteLockState = int.MinValue;
    private const int NoLockState = 0;
    private const int SingleReaderState = 1;

    private volatile int state;
    private uint version;    // volatile

    /// <summary>
    /// Returns a stamp that can be validated later.
    /// </summary>
    /// <returns>Optimistic read stamp. May be invalid.</returns>
    public readonly LockStamp TryOptimisticRead()
    {
        // Ordering of version and lock state must be respected:
        // Writer lock acquisition changes the state to Acquired and then increments the version.
        // Optimistic reader lock reads the version and then checks Acquired lock state to avoid false positives.
        var stamp = new LockStamp(in version);
        return state is WriteLockState ? default : stamp;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp.
    /// </summary>
    /// <param name="stamp">A stamp to check.</param>
    /// <returns><see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp; else <see langword="false"/>.</returns>
    public readonly bool Validate(in LockStamp stamp) => stamp.IsValid(in version) && state is not WriteLockState;

    /// <summary>
    /// Gets a value that indicates whether the current thread has entered the lock in write mode.
    /// </summary>
    public readonly bool IsWriteLockHeld => state is WriteLockState;

    /// <summary>
    /// Gets a value that indicates whether the current thread has entered the lock in read mode.
    /// </summary>
    public readonly bool IsReadLockHeld => state > NoLockState;

    /// <summary>
    /// Gets the total number of unique threads that have entered the lock in read mode.
    /// </summary>
    public readonly int CurrentReadCount => int.Max(0, state);

    /// <summary>
    /// Attempts to enter reader lock without blocking the caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if reader lock is acquired; otherwise, <see langword="false"/>.</returns>
    public bool TryEnterReadLock()
    {
        int currentState, nextState = state;
        do
        {
            currentState = nextState;
            if (currentState is WriteLockState or int.MaxValue)
                return false;
        }
        while ((nextState = Interlocked.CompareExchange(ref state, currentState + 1, currentState)) != currentState);

        return true;
    }

    /// <summary>
    /// Enters the lock in read mode.
    /// </summary>
    public void EnterReadLock()
    {
        for (var spinner = new SpinWait(); ; spinner.SpinOnce())
        {
            var currentState = state;
            if (currentState is not WriteLockState and not int.MaxValue && Interlocked.CompareExchange(ref state, currentState + 1, currentState) == currentState)
                break;
        }
    }

    /// <summary>
    /// Exits read mode.
    /// </summary>
    public void ExitReadLock() => Interlocked.Decrement(ref state);

    private bool TryEnterReadLock(in Timeout timeout, CancellationToken token)
    {
        for (var spinner = new SpinWait(); !timeout.IsExpired; token.ThrowIfCancellationRequested(), spinner.SpinOnce())
        {
            var currentState = state;
            if (currentState is not WriteLockState and not int.MaxValue && Interlocked.CompareExchange(ref state, currentState + 1, currentState) == currentState)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to enter the lock in read mode.
    /// </summary>
    /// <param name="timeout">The interval to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the calling thread entered read mode, otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public bool TryEnterReadLock(TimeSpan timeout, CancellationToken token = default)
        => TryEnterReadLock(new Timeout(timeout), token);

    /// <summary>
    /// Enters the lock in write mode.
    /// </summary>
    public void EnterWriteLock()
    {
        for (var spinner = new SpinWait();
             Interlocked.CompareExchange(ref state, WriteLockState, NoLockState) is not NoLockState;
             spinner.SpinOnce());

        Interlocked.Increment(ref version);
    }

    /// <summary>
    /// Attempts to enter writer lock without blocking the caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if writer lock is acquired; otherwise, <see langword="false"/>.</returns>
    public bool TryEnterWriteLock()
    {
        if (Interlocked.CompareExchange(ref state, WriteLockState, NoLockState) is not NoLockState)
            return false;

        Interlocked.Increment(ref version);
        return true;
    }

    private bool TryEnterWriteLock(in Timeout timeout, CancellationToken token)
    {
        for (var spinner = new SpinWait();
             Interlocked.CompareExchange(ref state, WriteLockState, NoLockState) is not NoLockState;
             token.ThrowIfCancellationRequested(), spinner.SpinOnce())
        {
            if (timeout)
                return false;
        }

        Interlocked.Increment(ref version);
        return true;
    }

    /// <summary>
    /// Tries to enter the lock in write mode.
    /// </summary>
    /// <param name="timeout">The interval to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the calling thread entered read mode, otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public bool TryEnterWriteLock(TimeSpan timeout, CancellationToken token = default)
        => TryEnterWriteLock(new Timeout(timeout), token);

    /// <summary>
    /// Exits the writer lock.
    /// </summary>
    public void ExitWriteLock() => state = NoLockState;

    /// <summary>
    /// Upgrades a reader lock to the writer lock.
    /// </summary>
    /// <remarks>
    /// The caller must have acquired reader lock. Otherwise, the behavior is unspecified.
    /// The method releases the reader lock and tries to obtain the writer lock.
    /// </remarks>
    public void UpgradeToWriteLock()
    {
        ExitReadLock();
        EnterWriteLock();
    }

    /// <summary>
    /// Attempts to upgrade a reader lock to the writer lock.
    /// </summary>
    /// <remarks>
    /// The method releases the reader lock even if it returns <see langword="false"/>, so the caller is responsible
    /// to reacquire the reader lock.
    /// </remarks>
    /// <returns><see langword="true"/> if the caller upgraded successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryUpgradeToWriteLock()
    {
        ExitReadLock();
        return TryEnterWriteLock();
    }

    /// <summary>
    /// Attempts to upgrade a reader lock to the writer lock.
    /// </summary>
    /// <remarks>
    /// The method releases the reader lock even if it returns <see langword="false"/>, so the caller is responsible
    /// to reacquire the reader lock.
    /// </remarks>
    /// <param name="timeout">The time to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the caller upgraded successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public bool TryUpgradeToWriteLock(TimeSpan timeout, CancellationToken token = default)
    {
        ExitReadLock();
        return TryEnterWriteLock(new Timeout(timeout), token);
    }

    /// <summary>
    /// Downgrades a writer lock back to the reader lock.
    /// </summary>
    public void DowngradeFromWriteLock() => state = SingleReaderState;
}