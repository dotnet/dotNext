using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents asynchronous mutually exclusive lock.
/// </summary>
[DebuggerDisplay($"IsLockHeld = {{{nameof(IsLockHeld)}}}")]
public class AsyncExclusiveLock : QueuedSynchronizer, IAsyncDisposable
{
    [StructLayout(LayoutKind.Auto)]
    private struct LockManager : ILockManager, IConsumer<WaitNode>
    {
        // null - not acquired, Sentinel.Instance - acquired asynchronously, Thread - acquired synchronously
        private bool state;

        internal readonly bool Value => state;

        internal readonly bool VolatileRead() => Volatile.Read(in state);

        public readonly bool IsLockAllowed => !state;

        public void AcquireLock()
            => state = true;

        internal void ExitLock() => state = false;

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node)
        {
        }
    }

    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;
    private LockManager manager;
    private Thread? lockOwner;

    /// <summary>
    /// Initializes a new asynchronous exclusive lock.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncExclusiveLock(int concurrencyLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);

        pool = new(OnCompleted, concurrencyLevel);
    }

    /// <summary>
    /// Initializes a new asynchronous exclusive lock.
    /// </summary>
    public AsyncExclusiveLock()
    {
        pool = new(OnCompleted);
    }

    private void OnCompleted(WaitNode node) => ReturnNode(ref pool, node);

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => waitQueueVisitor.SignalAll(ref manager);

    /// <summary>
    /// Indicates that exclusive lock taken.
    /// </summary>
    public bool IsLockHeld => manager.VolatileRead();

    /// <summary>
    /// Attempts to obtain exclusive lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryAcquireCore();
    }

    private bool IsLockHelpByCurrentThread
    {
        get => lockOwner is { } owner && ReferenceEquals(owner, Thread.CurrentThread);
        set
        {
            if (value)
                lockOwner = Thread.CurrentThread;
        }
    }

    private bool TryAcquireCore()
    {
        Monitor.Enter(SyncRoot);
        var result = IsLockHelpByCurrentThread = TryAcquire(ref manager);
        Monitor.Exit(SyncRoot);

        return result;
    }

    /// <summary>
    /// Tries to acquire the lock synchronously.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lock is acquired in timely manner; <see langword="false"/> if canceled or timed out.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="LockRecursionException">The lock is already acquired by the current thread.</exception>
    public bool TryAcquire(TimeSpan timeout, CancellationToken token = default)
    {
        if (IsLockHelpByCurrentThread)
            throw new LockRecursionException();
        
        bool result;
        try
        {
            IsLockHelpByCurrentThread = result = TryAcquireAsync(timeout, token).Wait();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Enters the lock in exclusive mode asynchronously.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask AcquireAsync(TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Enters the lock in exclusive mode asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask AcquireAsync(CancellationToken token = default)
        => AcquireAsync(ref pool, ref manager, new CancellationTokenOnly(token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see exception="LockStolenException"/> will be thrown for each suspended caller in the queue.
    /// The method cannot interrupt the caller that has already acquired the lock. If there is no suspended callers
    /// in the queue, this method is equivalent to <see cref="TryAcquireAsync(TimeSpan, CancellationToken)"/>.
    /// </remarks>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask<bool> TryStealAsync(object? reason, TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref manager, new TimeoutAndInterruptionReasonAndCancellationToken(reason, timeout, token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see exception="LockStolenException"/> will be thrown for each suspended caller in the queue.
    /// The method cannot interrupt the caller that has already acquired the lock. If there is no suspended callers
    /// in the queue, this method is equivalent to <see cref="TryAcquireAsync(TimeSpan, CancellationToken)"/>.
    /// </remarks>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask StealAsync(object? reason, TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref manager, new TimeoutAndInterruptionReasonAndCancellationToken(reason, timeout, token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see exception="LockStolenException"/> will be thrown for each suspended caller in the queue.
    /// The method cannot interrupt the caller that has already acquired the lock. If there is no suspended callers
    /// in the queue, this method is equivalent to <see cref="TryAcquireAsync(TimeSpan, CancellationToken)"/>.
    /// </remarks>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask StealAsync(object? reason = null, CancellationToken token = default)
        => AcquireAsync(ref pool, ref manager, new InterruptionReasonAndCancellationToken(reason, token));

    /// <summary>
    /// Releases previously acquired exclusive lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Release()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        ManualResetCompletionSource? suspendedCaller;
        lock (SyncRoot)
        {
            if (!manager.Value)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            manager.ExitLock();
            lockOwner = null;
            suspendedCaller = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
            {
                Dispose(true);
            }
        }

        suspendedCaller?.Resume();
    }

    private protected sealed override bool IsReadyToDispose => manager is { Value: false } && IsEmptyQueue;
    
    private protected new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>, IWaitNode
    {
        protected override void AfterConsumed() => AfterConsumed(this);

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }

        static bool IWaitNode.DrainOnReturn => true;
    }
}