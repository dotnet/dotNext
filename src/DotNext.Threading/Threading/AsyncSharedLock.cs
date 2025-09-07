using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

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
    private new sealed class WaitNode :
        QueuedSynchronizer.WaitNode,
        INodeMapper<WaitNode, bool>
    {
        internal bool IsStrongLock;

        static bool INodeMapper<WaitNode, bool>.GetValue(WaitNode node)
            => node.IsStrongLock;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        private const long ExclusiveMode = -1L;

        internal readonly long ConcurrencyLevel;
        private long remainingLocks; // -1 means that the lock is acquired in exclusive mode

        internal State(long concurrencyLevel) => ConcurrencyLevel = remainingLocks = concurrencyLevel;

        internal readonly long RemainingLocks => Atomic.Read(in remainingLocks);

        internal readonly bool IsWeakLockAllowed => remainingLocks > 0L;

        internal void AcquireWeakLock() => Interlocked.Decrement(ref remainingLocks);

        internal void ExitLock(bool downgrade)
        {
            remainingLocks = remainingLocks < 0L
                ? ConcurrencyLevel - Unsafe.BitCast<bool, byte>(downgrade)
                : remainingLocks + 1L;
        }

        internal readonly bool IsStrongLockHeld => remainingLocks < 0L;

        internal readonly bool IsStrongLockAllowed => remainingLocks == ConcurrencyLevel;

        internal void AcquireStrongLock() => remainingLocks = ExclusiveMode;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct WeakLockManager : ILockManager, IConsumer<WaitNode>
    {
        private State state;

        readonly bool ILockManager.IsLockAllowed
            => state.IsWeakLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireWeakLock();

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node)
        {
            node.IsStrongLock = false;
            node.DrainOnReturn = true;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct StrongLockManager : ILockManager, IConsumer<WaitNode>
    {
        private State state;

        readonly bool ILockManager.IsLockAllowed
            => state.IsStrongLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireStrongLock();

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node)
            => node.IsStrongLock = node.DrainOnReturn = true;
    }

    private State state;

    /// <summary>
    /// Initializes a new shared lock.
    /// </summary>
    /// <param name="concurrencyLevel">The number of unique callers that can obtain shared lock simultaneously.</param>
    /// <param name="limitedConcurrency">
    /// <see langword="true"/> if the potential number of concurrent flows will not be greater than <paramref name="concurrencyLevel"/>;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
    public AsyncSharedLock(int concurrencyLevel, bool limitedConcurrency = true)
        : base(limitedConcurrency ? concurrencyLevel : null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);

        state = new(concurrencyLevel);
    }

    private bool Signal(ref WaitQueueVisitor waitQueueVisitor, bool strongLock) => strongLock
        ? waitQueueVisitor.Signal(ref GetLockManager<StrongLockManager>())
        : waitQueueVisitor.Signal(ref GetLockManager<WeakLockManager>());

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
    {
        while (!waitQueueVisitor.IsEndOfQueue<WaitNode, bool>(out var strongLock) && Signal(ref waitQueueVisitor, strongLock))
        {
            waitQueueVisitor.Advance();
        }
    }

    /// <summary>
    /// Gets the number of shared locks that can be acquired.
    /// </summary>
    public long RemainingCount => Math.Max(state.RemainingLocks, 0L);

    /// <summary>
    /// Gets the maximum number of locks that can be obtained simultaneously.
    /// </summary>
    public long ConcurrencyLevel => state.ConcurrencyLevel;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive or shared mode.
    /// </summary>
    public bool IsLockHeld => state.RemainingLocks < state.ConcurrencyLevel;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive mode.
    /// </summary>
    public bool IsStrongLockHeld => state.IsStrongLockHeld;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TLockManager GetLockManager<TLockManager>()
        where TLockManager : struct, ILockManager, IConsumer<WaitNode>
        => ref Unsafe.As<State, TLockManager>(ref state);

    private bool TryAcquire<TManager>()
        where TManager : struct, ILockManager, IConsumer<WaitNode>
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = TryAcquire(ref GetLockManager<TManager>());
        Monitor.Exit(SyncRoot);

        return result;
    }

    /// <summary>
    /// Attempts to obtain lock synchronously without blocking caller thread.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryAcquire(bool strongLock)
        => strongLock ? TryAcquire<StrongLockManager>() : TryAcquire<WeakLockManager>();

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
        var options = new TimeoutAndCancellationToken(timeout, token);
        return strongLock
            ? TryAcquireAsync<WaitNode, StrongLockManager, TimeoutAndCancellationToken>(ref GetLockManager<StrongLockManager>(), options)
            : TryAcquireAsync<WaitNode, WeakLockManager, TimeoutAndCancellationToken>(ref GetLockManager<WeakLockManager>(), options);
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
        var options = new TimeoutAndCancellationToken(timeout, token);
        return strongLock
            ? AcquireAsync<WaitNode, StrongLockManager, TimeoutAndCancellationToken>(ref GetLockManager<StrongLockManager>(), options)
            : AcquireAsync<WaitNode, WeakLockManager, TimeoutAndCancellationToken>(ref GetLockManager<WeakLockManager>(), options);
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
        var options = new CancellationTokenOnly(token);
        return strongLock
            ? AcquireAsync<WaitNode, StrongLockManager, CancellationTokenOnly>(ref GetLockManager<StrongLockManager>(), options)
            : AcquireAsync<WaitNode, WeakLockManager, CancellationTokenOnly>(ref GetLockManager<WeakLockManager>(), options);
    }

    /// <summary>
    /// Releases the acquired weak lock or downgrade exclusive lock to the weak lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Downgrade()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock(downgrade: true);
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Releases the acquired lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Release()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock(downgrade: false);
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    private protected sealed override bool IsReadyToDispose => state.IsStrongLockAllowed && IsEmptyQueue;
}