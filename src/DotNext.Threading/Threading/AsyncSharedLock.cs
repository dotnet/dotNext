using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents a lock that can be acquired in exclusive or weak mode.
/// </summary>
/// <remarks>
/// This lock represents the combination of semaphore and reader-writer
/// lock. The caller can acquire weak locks simultaneously which count
/// is limited by the concurrency level passed into the constructor. However, the
/// only one caller can acquire the lock exclusively.
/// </remarks>
[DebuggerDisplay($"AvailableLocks = {{{nameof(RemainingCount)}}}, StrongLockHeld = {{{nameof(IsStrongLockHeld)}}}")]
public class AsyncSharedLock : QueuedSynchronizer, IAsyncDisposable
{
    private State state;

    /// <summary>
    /// Initializes a new shared lock.
    /// </summary>
    /// <param name="lockUpgradeThreshold">The number of unique callers that can obtain shared lock simultaneously.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockUpgradeThreshold"/> is less than 1.</exception>
    public AsyncSharedLock(long lockUpgradeThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lockUpgradeThreshold);

        state = new(ConcurrencyLevel = lockUpgradeThreshold);
    }

    private bool Signal(ref WaitQueueScope queue, bool strongLock) => strongLock
        ? queue.SignalCurrent<StrongLockManager>(new(ref state))
        : queue.SignalCurrent<WeakLockManager>(new(ref state));

    private protected sealed override void DrainWaitQueue(ref WaitQueueScope queue)
    {
        while (!queue.IsEndOfQueue<WaitNode, bool>(out var strongLock) && Signal(ref queue, strongLock))
        {
            queue.Advance();
        }
    }

    /// <summary>
    /// Gets the number of shared locks that can be acquired.
    /// </summary>
    public long RemainingCount => Math.Max(state.RemainingLocks, 0L);

    /// <summary>
    /// Gets the maximum number of locks that can be obtained simultaneously.
    /// </summary>
    public long LockUpgradeThreshold => state.Threshold;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive or shared mode.
    /// </summary>
    public bool IsLockHeld => state.RemainingLocks < state.Threshold;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive mode.
    /// </summary>
    public bool IsStrongLockHeld => state.IsStrongLockHeld;

    /// <summary>
    /// Attempts to obtain lock synchronously without blocking caller thread.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryAcquire(bool strongLock)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        
        (strongLock
                ? TryAcquire<StrongLockManager>(new(ref state), out var acquired)
                : TryAcquire<WeakLockManager>(new(ref state), out acquired))
            .Dispose();
        
        return acquired;
    }

    /// <summary>
    /// Attempts to enter the lock asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> TryAcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return strongLock
            ? EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, StrongLockManager>(ref builder, new(ref state))
            : EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, WeakLockManager>(ref builder, new(ref state));
    }

    /// <summary>
    /// Enters the lock asynchronously.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask AcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return strongLock
            ? EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, StrongLockManager>(ref builder, new(ref state))
            : EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, WeakLockManager>(ref builder, new(ref state));
    }

    /// <summary>
    /// Enters the lock asynchronously.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask AcquireAsync(bool strongLock, CancellationToken token = default)
    {
        var builder = BeginAcquisition(token);
        return strongLock
            ? EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, StrongLockManager>(ref builder, new(ref state))
            : EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, WeakLockManager>(ref builder, new(ref state));
    }

    /// <summary>
    /// Releases the acquired weak lock or downgrade exclusive lock to the weak lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Downgrade()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock(downgrade: true);
            DrainWaitQueue(ref queue);

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }
        finally
        {
            queue.Dispose();
        }
    }

    /// <summary>
    /// Releases the acquired lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Release()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock(downgrade: false);
            DrainWaitQueue(ref queue);

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }
        finally
        {
            queue.Dispose();
        }
    }

    private protected sealed override bool IsReadyToDispose => state.IsStrongLockAllowed && IsEmptyQueue;
    
    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IWaitNodeFeature<bool>
    {
        internal bool IsStrongLock;

        bool IWaitNodeFeature<bool>.Feature => IsStrongLock;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        private const long ExclusiveMode = -1L;

        internal readonly long Threshold;
        private long remainingLocks; // -1 means that the lock is acquired in exclusive mode

        internal State(long concurrencyLevel) => Threshold = remainingLocks = concurrencyLevel;

        internal readonly long RemainingLocks => Atomic.Read(in remainingLocks);

        internal readonly bool IsWeakLockAllowed => remainingLocks > 0L;

        internal void AcquireWeakLock() => Interlocked.Decrement(ref remainingLocks);

        internal void ExitLock(bool downgrade)
        {
            remainingLocks = remainingLocks < 0L
                ? Threshold - Unsafe.BitCast<bool, byte>(downgrade)
                : remainingLocks + 1L;
        }

        internal readonly bool IsStrongLockHeld => remainingLocks < 0L;

        internal readonly bool IsStrongLockAllowed => remainingLocks == Threshold;

        internal void AcquireStrongLock() => remainingLocks = ExclusiveMode;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct WeakLockManager(ref State state) : ILockManager<WaitNode>
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed
            => state.IsWeakLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireWeakLock();

        static void ILockManager<WaitNode>.Initialize(WaitNode node)
            => node.IsStrongLock = false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct StrongLockManager(ref State state) : ILockManager<WaitNode>
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed
            => state.IsStrongLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireStrongLock();

        static void ILockManager<WaitNode>.Initialize(WaitNode node)
            => node.IsStrongLock = true;
    }
}