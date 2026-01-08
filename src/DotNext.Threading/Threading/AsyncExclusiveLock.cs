using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;
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
        bool lockTaken;
        using (TryAcquire(new LockManager(ref acquired), out lockTaken))
        {
            IsLockHelpByCurrentThread = lockTaken;
        }

        return lockTaken;
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
            lockOwner = null;
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

    private protected sealed override bool IsReadyToDispose => !acquired && IsEmptyQueue;
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct LockManager(ref bool acquired) : ILockManager<WaitNode>
    {
        private readonly ref bool acquired = ref acquired;

        bool ILockManager.IsLockAllowed => !acquired;

        void ILockManager.AcquireLock() => acquired = true;
    }
}