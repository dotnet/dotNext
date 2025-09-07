using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Patterns;
using Tasks;

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

    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncTrigger(long concurrencyLevel)
        : base(concurrencyLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => waitQueueVisitor.SignalAll();

    private bool SignalCore<TVisitor>(TVisitor visitor)
        where TVisitor : struct, IWaitQueueVisitor
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        bool signaled;
        lock (SyncRoot)
        {
            signaled = DrainWaitQueue(visitor, out suspendedCallers);
        }

        suspendedCallers?.Unwind();
        return signaled;
    }

    private bool DrainWaitQueue<TVisitor>(TVisitor visitor, out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
        where TVisitor : struct, IWaitQueueVisitor
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var waitQueue = GetWaitQueue(ref detachedQueue);
        var signaled = visitor.Visit(ref waitQueue);
        suspendedCallers = detachedQueue.First;
        return signaled;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Reset() => false;

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

        return SignalCore(new ResumingVisitor(resumeAll));
    }

    /// <summary>
    /// Interrupts all the suspended callers.
    /// </summary>
    /// <param name="reason">The interruption reason.</param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public new bool Interrupt(object? reason)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return SignalCore(new InterruptingVisitor(reason));
    }

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => IsEmptyQueue;

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Signal();

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
        => TryAcquireAsync<WaitNode, DefaultLockManager<WaitNode>, TimeoutAndCancellationToken>(ref DefaultManager, new(timeout, token));

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
        => AcquireAsync<WaitNode, DefaultLockManager<WaitNode>, CancellationTokenOnly>(ref DefaultManager, new CancellationTokenOnly(token));

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
        ValueTask<bool> task;

        switch (timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                task = !SignalCore(new ResumingVisitor(resumeAll)) && throwOnEmptyQueue
                    ? ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                    : new(false);

                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                LinkedValueTaskCompletionSource<bool>? suspendedCallers;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    factory = !DrainWaitQueue(new ResumingVisitor(resumeAll), out suspendedCallers) && throwOnEmptyQueue
                        ? EmptyWaitQueueExceptionFactory.Instance
                        : EnqueueNode();
                }

                suspendedCallers?.Unwind();
                task = factory.Invoke(timeout, token);
                break;
        }

        return task;
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
        if (token.IsCancellationRequested)
            return ValueTask.FromCanceled(token);

        ISupplier<TimeSpan, CancellationToken, ValueTask> factory;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            factory = !DrainWaitQueue(new ResumingVisitor(resumeAll), out suspendedCallers) && throwOnEmptyQueue
                ? EmptyWaitQueueExceptionFactory.Instance
                : EnqueueNodeThrowOnTimeout();
        }

        suspendedCallers?.Unwind();
        return factory.Invoke(token);
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
    public ValueTask<bool> SpinWaitAsync<TCondition>(TCondition condition, TimeSpan timeout, CancellationToken token = default)
        where TCondition : ISupplier<bool>
        => SpinWaitAsync(new ConditionalLockManager<TCondition> { Condition = condition }, new(timeout), token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> SpinWaitAsync<TCondition>(ConditionalLockManager<TCondition> manager, Timeout timeout, CancellationToken token)
        where TCondition : ISupplier<bool>
    {
        do
        {
            if (manager.Condition.Invoke())
                return true;
        }
        while (timeout.TryGetRemainingTime(out var remainingTime) && await TryAcquireAsync<WaitNode, ConditionalLockManager<TCondition>, TimeoutAndCancellationToken>(ref manager, new(remainingTime, token)).ConfigureAwait(false));

        return false;
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
    public ValueTask SpinWaitAsync<TCondition>(TCondition condition, CancellationToken token = default)
        where TCondition : ISupplier<bool>
        => SpinWaitAsync(new ConditionalLockManager<TCondition> { Condition = condition }, new CancellationTokenOnly(token));

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SpinWaitAsync<TCondition>(ConditionalLockManager<TCondition> manager, CancellationTokenOnly options)
        where TCondition : ISupplier<bool>
    {
        while (!manager.Condition.Invoke())
            await AcquireAsync<WaitNode, ConditionalLockManager<TCondition>, CancellationTokenOnly>(ref manager, options).ConfigureAwait(false);
    }

    [StructLayout(LayoutKind.Auto)]
    private struct ConditionalLockManager<TCondition> : ILockManager, IConsumer<WaitNode>
        where TCondition : ISupplier<bool>
    {
        internal required TCondition Condition;

        bool ILockManager.IsLockAllowed => Condition.Invoke();

        readonly void ILockManager.AcquireLock()
        {
        }

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;
    }

    private sealed class EmptyWaitQueueExceptionFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>, ISingleton<EmptyWaitQueueExceptionFactory>
    {
        public static EmptyWaitQueueExceptionFactory Instance { get; } = new();

        private EmptyWaitQueueExceptionFactory()
        {
        }

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken tpken)
            => ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken tpken)
            => ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));
    }
    
    private interface IWaitQueueVisitor
    {
        bool Visit(ref WaitQueueVisitor waitQueueVisitor);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ResumingVisitor(bool resumeAll) : IWaitQueueVisitor
    {
        bool IWaitQueueVisitor.Visit(ref WaitQueueVisitor waitQueueVisitor)
        {
            bool signaled;
            if (resumeAll)
            {
                waitQueueVisitor.SignalAll(out signaled);
            }
            else
            {
                waitQueueVisitor.SignalFirst(out signaled);
            }

            return signaled;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct InterruptingVisitor : IWaitQueueVisitor
    {
        private readonly PendingTaskInterruptedException exception;

        public InterruptingVisitor(object? reason)
            => ExceptionDispatchInfo.SetCurrentStackTrace(exception = new() { Reason = reason });

        bool IWaitQueueVisitor.Visit(ref WaitQueueVisitor waitQueueVisitor)
        {
            waitQueueVisitor.SignalAll(exception, out bool signaled);
            return signaled;
        }
    }
}