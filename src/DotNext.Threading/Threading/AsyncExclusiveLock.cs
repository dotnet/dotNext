using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Represents asynchronous mutually exclusive lock.
/// </summary>
[DebuggerDisplay($"IsLockHeld = {{{nameof(IsLockHeld)}}}")]
public class AsyncExclusiveLock : QueuedSynchronizer, IAsyncDisposable
{
    private bool acquired;
    private Thread? lockOwner;

    /// <summary>
    /// Initializes a new asynchronous exclusive lock.
    /// </summary>
    public AsyncExclusiveLock()
    {
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueScope queue)
        => queue.SignalAll(new LockManager(ref acquired));

    /// <summary>
    /// Indicates that exclusive lock taken.
    /// </summary>
    public bool IsLockHeld => Volatile.Read(in acquired);

    /// <summary>
    /// Attempts to obtain exclusive lock synchronously without blocking caller thread.
    /// </summary>
    /// <remarks>
    /// This method doesn't set thread ownership for this lock. Use <see cref="TryAcquire(TimeSpan, CancellationToken)"/>
    /// with <see cref="TimeSpan.Zero"/> argument instead.
    /// </remarks>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        TryAcquire(new LockManager(ref acquired), out var lockTaken).Dispose();
        return lockTaken;
    }

    private bool IsLockHeldByCurrentThread
    {
        get => lockOwner is { } owner && ReferenceEquals(owner, Thread.CurrentThread);
        set => lockOwner = value ? Thread.CurrentThread : null;
    }

    /// <summary>
    /// Tries to acquire the lock synchronously.
    /// </summary>
    /// <remarks>
    /// Do not release the lock acquired by this method in another thread.
    /// </remarks>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lock is acquired in timely manner; <see langword="false"/> if canceled or timed out.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="LockRecursionException">The lock is already acquired by the current thread.</exception>
    public bool TryAcquire(TimeSpan timeout, CancellationToken token = default)
    {
        if (IsLockHeldByCurrentThread)
            throw new LockRecursionException();
        
        bool result;
        try
        {
            result = TryAcquireAsync(timeout, token).Wait();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            result = false;
        }

        return IsLockHeldByCurrentThread = result;
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
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, LockManager>(ref builder, new(ref acquired));
    }

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
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, LockManager>(ref builder, new(ref acquired));
    }

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
    {
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, LockManager>(ref builder, new(ref acquired));
    }

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see cref="PendingTaskInterruptedException"/> will be thrown for each suspended caller in the queue.
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
    {
        var exception = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, LockManager>(
            exception,
            ref builder,
            new(ref acquired));
    }

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see cref="PendingTaskInterruptedException"/> will be thrown for each suspended caller in the queue.
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
    {
        var exception = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, LockManager>(
            exception,
            ref builder,
            new(ref acquired));
    }

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires the lock.
    /// </summary>
    /// <remarks>
    /// <see cref="PendingTaskInterruptedException"/> will be thrown for each suspended caller in the queue.
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
    {
        var exception = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, LockManager>(
            exception,
            ref builder,
            new(ref acquired));
    }

    /// <summary>
    /// Releases previously acquired exclusive lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public void Release()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            if (!acquired)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            acquired = false;
            IsLockHeldByCurrentThread = false;
            DrainWaitQueue(ref queue);

            if (IsDisposing && IsReadyToDispose)
            {
                Dispose(true);
            }
        }
        finally
        {
            queue.Dispose();
        }
    }

    /// <summary>
    /// Temporarily releases the lock to let the suspended callers in, and then reacquires it.
    /// </summary>
    /// <remarks>
    /// If there are no suspended callers, the method completes synchronously and the lock remains held by the caller.
    /// Otherwise, the lock is passed to the first suspended caller, and the current caller is placed at the head
    /// of the wait queue. Thus, it reacquires the lock ahead of the callers suspended by
    /// <see cref="AcquireAsync(CancellationToken)"/> and similar methods.
    /// Callers that keep yielding to each other in a loop can starve the callers suspended by regular acquisition methods.
    /// <br/>
    /// The caller must restore the invariants of the protected state before calling this method, because
    /// other callers can observe and modify that state while the lock is yielded.
    /// <br/>
    /// If reacquisition is canceled or interrupted, the method throws an exception, and the lock is no longer
    /// held by the caller. In this case, the caller must not call <see cref="Release"/>: the lock might be already
    /// acquired by another caller. Track the lock state explicitly to release the lock correctly:
    /// <code>
    /// var lockTaken = false;
    /// await exclusiveLock.AcquireAsync(token);
    /// lockTaken = true;
    /// try
    /// {
    ///     // ...
    ///     lockTaken = false;
    ///     await exclusiveLock.YieldAsync(token);
    ///     lockTaken = true;
    ///     // ...
    /// }
    /// finally
    /// {
    ///     if (lockTaken)
    ///         exclusiveLock.Release();
    /// }
    /// </code>
    /// </remarks>
    /// <param name="token">The token that can be used to abort lock reacquisition.</param>
    /// <returns>The task representing lock reacquisition.</returns>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="InvalidOperationException">
    /// The lock is acquired synchronously by <see cref="TryAcquire(TimeSpan, CancellationToken)"/>.
    /// In this case, the lock remains held by the caller.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object has been disposed. If the object is being disposed gracefully with <see cref="QueuedSynchronizer.DisposeAsync"/>,
    /// the lock is released and not held by the caller.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation has been canceled. The lock is not held by the caller.
    /// </exception>
    /// <exception cref="PendingTaskInterruptedException">
    /// The operation has been interrupted manually. The lock is not held by the caller.
    /// </exception>
    public ValueTask YieldAsync(CancellationToken token = default)
    {
        if (!IsDisposed)
        {
            var queue = CaptureWaitQueue();
            try
            {
                if (!acquired)
                    return ValueTask.FromException(new SynchronizationLockException(ExceptionMessages.NotInLock));

                if (IsLockHeldByCurrentThread)
                    return ValueTask.FromException(new InvalidOperationException(ExceptionMessages.LockAcquiredSynchronously));

                acquired = false;
                DrainWaitQueue(ref queue);

                if (!IsDisposing)
                    return Yield<WaitNode, LockManager>(new(ref acquired), token);

                // graceful disposal is in progress: give up the lock, as Release does
                if (IsReadyToDispose)
                    Dispose(true);
            }
            finally
            {
                queue.Dispose();
            }
        }

        return new(DisposedTask);
    }

    private protected sealed override bool IsReadyToDispose => !acquired && IsEmptyQueue;
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct LockManager(ref bool acquired) : ILockManager<WaitNode>
    {
        private readonly ref bool acquired = ref acquired;

        bool ILockManager.IsLockAllowed => !acquired;

        void ILockManager.AcquireLock() => acquired = true;
    }
}