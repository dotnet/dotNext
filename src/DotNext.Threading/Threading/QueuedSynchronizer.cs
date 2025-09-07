using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
/// </summary>
/// <remarks>
/// This class is designed to provide better throughput rather than optimized response time.
/// It means that it minimizes contention between concurrent calls and allows to process
/// as many concurrent requests as possible by the cost of the execution time of a single
/// method for a particular caller.
/// </remarks>
public abstract partial class QueuedSynchronizer : Disposable
{
    private readonly TaskCompletionSource disposeTask;

    private protected QueuedSynchronizer(long? concurrencyLevel = null)
    {
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        waitQueue = new()
        {
            MeasurementTags = new()
            {
                { LockTypeMeterAttribute, GetType().Name }
            },
        };

        pool = new(concurrencyLevel);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected object SyncRoot => disposeTask;

    /// <summary>
    /// Cancels all suspended callers.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void CancelSuspendedCallers(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
            throw new ArgumentOutOfRangeException(nameof(token));

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        var exception = new OperationCanceledException(token);
        ExceptionDispatchInfo.SetCurrentStackTrace(exception);
        
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue(exception);
        }

        suspendedCallers?.Unwind();
    }

    private void NotifyObjectDisposed(Exception? reason = null)
    {
        if (reason is null)
        {
            ExceptionDispatchInfo.SetCurrentStackTrace(reason = CreateException());
        }

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue(reason);
        }

        suspendedCallers?.Unwind();
    }

    private protected LinkedValueTaskCompletionSource<bool>? Interrupt(object? reason = null)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        
        var exception = new PendingTaskInterruptedException { Reason = reason };
        ExceptionDispatchInfo.SetCurrentStackTrace(exception);

        return DrainWaitQueue(exception);
    }

    private void Dispose(bool disposing, Exception? reason)
    {
        if (disposing)
        {
            NotifyObjectDisposed(reason);
            disposeTask.TrySetResult();
            callerInfo?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe and may not be used concurrently with other members of this instance.
    /// </remarks>
    /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
    protected override void Dispose(bool disposing)
        => Dispose(disposing, reason: null);

    /// <summary>
    /// Releases all resources associated with this object.
    /// </summary>
    /// <param name="reason">The exception to be passed to all suspended callers.</param>
    public void Dispose(Exception? reason)
    {
        Dispose(TryBeginDispose(), reason);
        GC.SuppressFinalize(this);
    }

    private protected virtual bool IsReadyToDispose => true;

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
    {
        ValueTask result;

        lock (SyncRoot)
        {
            if (this is { IsReadyToDispose: true, IsDisposed: false })
            {
                Dispose(true);
                result = ValueTask.CompletedTask;
            }
            else
            {
                result = new(disposeTask.Task);
            }
        }

        return result;
    }

    /// <summary>
    /// Disposes this synchronization primitive gracefully.
    /// </summary>
    /// <returns>The task representing asynchronous result.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}