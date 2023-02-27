using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    private struct LockManager : ILockManager<DefaultWaitNode>
    {
        private bool state;

        internal readonly bool Value => state;

        internal readonly bool VolatileRead() => Volatile.Read(ref Unsafe.AsRef(in state));

        public readonly bool IsLockAllowed => !state;

        public void AcquireLock() => state = true;

        internal void ExitLock() => state = false;

        readonly void ILockManager<DefaultWaitNode>.InitializeNode(DefaultWaitNode node)
        {
            // nothing to do here
        }
    }

    private ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool;
    private LockManager manager;

    /// <summary>
    /// Initializes a new asynchronous exclusive lock.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncExclusiveLock(int concurrencyLevel)
    {
        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        pool = new(OnCompleted, concurrencyLevel);
    }

    /// <summary>
    /// Initializes a new asynchronous exclusive lock.
    /// </summary>
    public AsyncExclusiveLock()
    {
        pool = new(OnCompleted);
    }

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(DefaultWaitNode node)
    {
        if (node.NeedsRemoval && RemoveNode(node))
            DrainWaitQueue();

        pool.Return(node);
    }

    /// <summary>
    /// Indicates that exclusive lock taken.
    /// </summary>
    public bool IsLockHeld => manager.VolatileRead();

    /// <summary>
    /// Attempts to obtain exclusive lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TryAcquire()
    {
        ThrowIfDisposed();
        return TryAcquire(ref manager);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ISupplier<TimeSpan, CancellationToken, TResult> Acquire<TResult>()
        where TResult : struct, IEquatable<TResult>
        => GetTaskFactory<DefaultWaitNode, LockManager, TResult>(ref manager, ref pool);

    /// <summary>
    /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask<bool> task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = new(TryAcquire());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<bool>(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled<bool>(token)
                    : Acquire<ValueTask<bool>>().Invoke(timeout, token);
                break;
        }

        return task;
    }

    /// <summary>
    /// Enters the lock in exclusive mode asynchronously.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask AcquireAsync(TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = TryAcquire()
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(new TimeoutException());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled(token)
                    : Acquire<ValueTask>().Invoke(timeout, token);
                break;
        }

        return task;
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
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : Acquire<ValueTask>().Invoke(token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ISupplier<TimeSpan, CancellationToken, TResult> Steal<TResult>(object? reason)
        where TResult : struct, IEquatable<TResult>
    {
        Interrupt(reason);
        return GetTaskFactory<DefaultWaitNode, LockManager, TResult>(ref manager, ref pool);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private bool TrySteal(object? reason)
    {
        Interrupt(reason);
        return TryAcquire(ref manager);
    }

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
    {
        ValueTask<bool> task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = new(TrySteal(reason));
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<bool>(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled<bool>(token)
                    : Steal<ValueTask<bool>>(reason).Invoke(timeout, token);
                break;
        }

        return task;
    }

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
    {
        ValueTask task;

        switch (timeout.Ticks)
        {
            case < 0L and not Timeout.InfiniteTicks:
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    task = TrySteal(reason)
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(new TimeoutException());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }

                break;
            default:
                task = token.IsCancellationRequested
                    ? ValueTask.FromCanceled(token)
                    : Steal<ValueTask>(reason).Invoke(timeout, token);
                break;
        }

        return task;
    }

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
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : Steal<ValueTask>(reason).Invoke(token);

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));

        for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
        {
            next = current.Next;

            if (!manager.IsLockAllowed)
                break;

            // skip dead node
            if (RemoveAndSignal(current))
            {
                manager.AcquireLock();
                break;
            }
        }
    }

    /// <summary>
    /// Releases previously acquired exclusive lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Release()
    {
        ThrowIfDisposed();
        if (!manager.Value)
            throw new SynchronizationLockException(ExceptionMessages.NotInLock);

        manager.ExitLock();
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    private protected sealed override bool IsReadyToDispose => manager is { Value: false } && first is null;
}