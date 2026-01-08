using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents asynchronous trigger that allows to resume and suspend
/// concurrent flows.
/// </summary>
public class AsyncTrigger : QueuedSynchronizer, IAsyncEvent
{
    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    public AsyncTrigger()
    {
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Reset() => false;

    private bool Signal(bool resumeAll, ref WaitQueueScope queue)
    {
        bool signaled;
        if (resumeAll)
        {
            queue.SignalAll(out signaled);
        }
        else
        {
            queue.SignalFirst(out signaled);
        }

        return signaled;
    }

    /// <summary>
    /// Resumes the first suspended caller in the wait queue.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public bool Signal(bool resumeAll = false)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            return Signal(resumeAll, ref queue);
        }
        finally
        {
            queue.Dispose();
        }
    }

    /// <summary>
    /// Interrupts all the suspended callers.
    /// </summary>
    /// <param name="reason">The interruption reason.</param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public bool Interrupt(object? reason)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var e = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);

        using var queue = CaptureWaitQueue();
        queue.SignalAll(e, out var signaled);
        return signaled;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => false;

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Signal();

    private T WaitAsync<T, TBuilder>(ref TBuilder builder)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        if (!builder.IsCompleted)
        {
            Acquire<T, TBuilder, WaitNode>(ref builder, acquired: false);
        }

        return builder.Build();
    }

    /// <summary>
    /// Suspends the caller and waits for the signal.
    /// </summary>
    /// <remarks>
    /// This method always suspends the caller.
    /// </remarks>
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="Signal(bool)"/>
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return WaitAsync<ValueTask<bool>, TimeoutAndCancellationToken>(ref builder);
    }

    /// <summary>
    /// Suspends the caller and waits for the signal.
    /// </summary>
    /// <remarks>
    /// This method always suspends the caller.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="Signal(bool)"/>
    public ValueTask WaitAsync(CancellationToken token = default)
    {
        var builder = BeginAcquisition(token);
        return WaitAsync<ValueTask, CancellationTokenOnly>(ref builder);
    }

    /// <summary>
    /// Resumes the first suspended caller in the queue and suspends the immediate caller.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <param name="throwOnEmptyQueue">
    /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
    /// <see langword="false"/> to suspend the caller.
    /// </param>
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
    public ValueTask<bool> SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return SignalAndWaitAsync<ValueTask<bool>, TimeoutAndCancellationToken>(
            ref builder,
            resumeAll,
            throwOnEmptyQueue
        );
    }

    /// <summary>
    /// Resumes the first suspended caller in the queue and suspends the immediate caller.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume all suspended callers in the queue;
    /// <see langword="false"/> to resume the first suspended caller in the queue.
    /// </param>
    /// <param name="throwOnEmptyQueue">
    /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
    /// <see langword="false"/> to suspend the caller.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
    public ValueTask SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, CancellationToken token = default)
    {
        var builder = BeginAcquisition(token);
        return SignalAndWaitAsync<ValueTask, CancellationTokenOnly>(
            ref builder,
            resumeAll,
            throwOnEmptyQueue);
    }

    private T SignalAndWaitAsync<T, TBuilder>(ref TBuilder builder, bool resumeAll, bool throwOnEmptyQueue)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, IWaitQueueProvider, allows ref struct
    {
        WaitQueueScope queue;
        if (builder.IsCompleted)
        {
            queue = default;
        }
        else
        {
            queue = builder.CaptureWaitQueue();
            if (Signal(resumeAll, ref queue))
            {
                Acquire<T, TBuilder, WaitNode>(ref builder, acquired: false);
            }
            else if (throwOnEmptyQueue)
            {
                builder.Complete<DefaultExceptionFactory<EmptyWaitQueueException>>();
            }
        }

        var task = builder.Build();
        queue.ResumeSuspendedCallers();
        return task;
    }

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="TCondition">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="timeout">The number of time to wait before this event is set.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public ValueTask<bool> SpinWaitAsync<TCondition>(TCondition condition, TimeSpan timeout, CancellationToken token = default)
        where TCondition : ISupplier<bool>
        => SpinWaitAsync(condition, new Timeout(timeout), token);

    private async ValueTask<bool> SpinWaitAsync<TCondition>(TCondition condition, Timeout timeout, CancellationToken token)
        where TCondition : ISupplier<bool>
    {
        do
        {
            if (condition.Invoke())
                return true;
        } while (timeout.TryGetRemainingTime(out var remainingTime) &&
                 await TryAcquireAsync(ref condition, remainingTime, token).ConfigureAwait(false));

        return false;
    }

    private ValueTask<bool> TryAcquireAsync<TCondition>(ref TCondition condition, TimeSpan timeout, CancellationToken token)
        where TCondition : ISupplier<bool>
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, ConditionalLockManager<TCondition>>(
            ref builder, new(ref condition));
    }

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="TCondition">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask SpinWaitAsync<TCondition>(TCondition condition, CancellationToken token = default)
        where TCondition : ISupplier<bool>
    {
        while (!condition.Invoke())
            await AcquireAsync(ref condition, token)
                .ConfigureAwait(false);
    }

    private ValueTask AcquireAsync<TCondition>(ref TCondition condition, CancellationToken token)
        where TCondition : ISupplier<bool>
    {
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, ConditionalLockManager<TCondition>>(
            ref builder, new(ref condition));
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ConditionalLockManager<TCondition>(ref TCondition condition) : ILockManager<WaitNode>
        where TCondition : ISupplier<bool>
    {
        private readonly ref TCondition condition = ref condition;
        
        bool ILockManager.IsLockAllowed => condition.Invoke();

        void ILockManager.AcquireLock()
        {
        }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "False positive.")]
    private sealed class EmptyWaitQueueException() : InvalidOperationException(ExceptionMessages.EmptyWaitQueue);
}