using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents asynchronous trigger that allows to resume and suspend
/// concurrent flows.
/// </summary>
public class AsyncTrigger : QueuedSynchronizer, IAsyncEvent
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct LockManager : ILockManager<DefaultWaitNode>
    {
        bool ILockManager.IsLockAllowed => false;

        void ILockManager.AcquireLock()
        {
            // nothing to do here
        }

        void ILockManager<DefaultWaitNode>.InitializeNode(DefaultWaitNode node)
        {
            // nothing to do here
        }
    }

    private static LockManager manager;
    private ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool;

    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    public AsyncTrigger()
    {
        pool = new(OnCompleted);
    }

    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncTrigger(int concurrencyLevel)
    {
        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        pool = new(OnCompleted, concurrencyLevel);
    }

    private void OnCompleted(DefaultWaitNode node)
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                RemoveNode(node);

            pool.Return(node);
        }
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Reset() => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LinkedValueTaskCompletionSource<bool>? Detach(bool detachAll)
        => detachAll ? DetachWaitQueue() : DetachHead();

    /// <summary>
    /// Resumes the first suspended caller in the wait queue.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume the first suspended caller in the queue;
    /// <see langword="false"/> to resume all suspended callers in the queue.
    /// </param>
    /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public bool Signal(bool resumeAll = false)
    {
        ThrowIfDisposed();

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = Detach(resumeAll)?.SetResult(true);
        }

        if (suspendedCallers is not null)
        {
            suspendedCallers.Unwind();
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => first is null;

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
        => TryAcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));

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
        => AcquireAsync(ref pool, ref manager, new CancellationTokenOnly(token));

    /// <summary>
    /// Resumes the first suspended caller in the queue and suspends the immediate caller.
    /// </summary>
    /// <param name="resumeAll">
    /// <see langword="true"/> to resume the first suspended caller in the queue;
    /// <see langword="false"/> to resume all suspended callers in the queue.
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
            case < 0L:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                LinkedValueTaskCompletionSource<bool>? suspendedCallers;
                lock (SyncRoot)
                {
                    suspendedCallers = Detach(resumeAll)?.SetResult(true);
                }

                if (suspendedCallers is not null)
                {
                    suspendedCallers.Unwind();
                }
                else if (throwOnEmptyQueue)
                {
                    task = ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));
                    break;
                }

                task = new(false);
                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    suspendedCallers = Detach(resumeAll)?.SetResult(true);
                    factory = suspendedCallers is null && throwOnEmptyQueue
                        ? EmptyWaitQueueExceptionFactory.Instance
                        : EnqueueNode(ref pool, ref manager, throwOnTimeout: false);
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
    /// <see langword="true"/> to resume the first suspended caller in the queue;
    /// <see langword="false"/> to resume all suspended callers in the queue.
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
            suspendedCallers = Detach(resumeAll)?.SetResult(true);
            factory = suspendedCallers is null && throwOnEmptyQueue
                ? EmptyWaitQueueExceptionFactory.Instance
                : EnqueueNode(ref pool, ref manager, throwOnTimeout: true);
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
        where TCondition : notnull, ISupplier<bool>
        => SpinWaitAsync(new ConditionalLockManager<TCondition> { Condition = condition }, new(timeout), token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> SpinWaitAsync<TCondition>(ConditionalLockManager<TCondition> manager, Timeout timeout, CancellationToken token)
        where TCondition : notnull, ISupplier<bool>
    {
        do
        {
            if (manager.Condition.Invoke())
                return true;
        }
        while (timeout.RemainingTime.TryGetValue(out var remainingTime) && await TryAcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(remainingTime, token)).ConfigureAwait(false));

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
        where TCondition : notnull, ISupplier<bool>
        => SpinWaitAsync(new ConditionalLockManager<TCondition> { Condition = condition }, new CancellationTokenOnly(token));

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SpinWaitAsync<TCondition>(ConditionalLockManager<TCondition> manager, CancellationTokenOnly options)
        where TCondition : notnull, ISupplier<bool>
    {
        while (!manager.Condition.Invoke())
            await AcquireAsync(ref pool, ref manager, options).ConfigureAwait(false);
    }

    [StructLayout(LayoutKind.Auto)]
    private struct ConditionalLockManager<TCondition> : ILockManager<DefaultWaitNode>
        where TCondition : notnull, ISupplier<bool>
    {
        internal TCondition Condition;

        bool ILockManager.IsLockAllowed => Condition.Invoke();

        readonly void ILockManager.AcquireLock()
        {
        }

        readonly void ILockManager<DefaultWaitNode>.InitializeNode(DefaultWaitNode node)
        {
        }
    }

    private sealed class EmptyWaitQueueExceptionFactory : ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>
    {
        internal static readonly EmptyWaitQueueExceptionFactory Instance = new();

        private EmptyWaitQueueExceptionFactory()
        {
        }

        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken tpken)
            => ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));

        ValueTask<bool> ISupplier<TimeSpan, CancellationToken, ValueTask<bool>>.Invoke(TimeSpan timeout, CancellationToken tpken)
            => ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));
    }
}

/// <summary>
/// Represents asynchronous trigger that allows to resume and suspend
/// concurrent flows.
/// </summary>
/// <typeparam name="TState">The type of the state used for coordination.</typeparam>
[Obsolete("Use QueuedSynchronizer<T> instead.")]
public class AsyncTrigger<TState> : QueuedSynchronizer
    where TState : class
{
    /// <summary>
    /// Represents state transition.
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// Tests whether the state can be changed.
        /// </summary>
        /// <param name="state">The state to check.</param>
        /// <returns><see langword="true"/> if transition is allowed; otherwise, <see langword="false"/>.</returns>
        bool Test(TState state);

        /// <summary>
        /// Do transition.
        /// </summary>
        /// <param name="state">The state to modify.</param>
        void Transit(TState state);
    }

    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>
    {
        internal ITransition? Transition;

        protected override void AfterConsumed() => AfterConsumed(this);

        protected override void Cleanup()
        {
            Transition = null;
            base.Cleanup();
        }

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct LockManager : ILockManager<WaitNode>
    {
        private readonly ITransition transition;
        private readonly TState state;

        internal LockManager(TState state, ITransition transition)
        {
            this.transition = transition;
            this.state = state;
        }

        bool ILockManager.IsLockAllowed => transition.Test(state);

        void ILockManager.AcquireLock() => transition.Transit(state);

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Transition = transition;
    }

    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;

    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    /// <param name="state">The coordination state.</param>
    public AsyncTrigger(TState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        pool = new(OnCompleted);
    }

    /// <summary>
    /// Initializes a new trigger.
    /// </summary>
    /// <param name="state">The coordination state.</param>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncTrigger(TState state, int concurrencyLevel)
    {
        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        State = state ?? throw new ArgumentNullException(nameof(state));
        pool = new(OnCompleted, concurrencyLevel);
    }

    private void OnCompleted(WaitNode node)
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = node.NeedsRemoval && RemoveNode(node)
                ? DrainWaitQueue()
                : null;

            pool.Return(node);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Gets state of this trigger.
    /// </summary>
    public TState State { get; }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        Debug.Assert(first is null or WaitNode);

        LinkedValueTaskCompletionSource<bool>? localFirst = null, localLast = null;

        for (WaitNode? current = Unsafe.As<WaitNode>(first), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or WaitNode);

            next = Unsafe.As<WaitNode>(current.Next);

            if (current.IsCompleted || current.Transition is not { } transition)
            {
                RemoveNode(current);
                continue;
            }

            if (!transition.Test(State))
                break;

            if (RemoveAndSignal(current))
            {
                transition.Transit(State);
                LinkedValueTaskCompletionSource<bool>.Append(ref localFirst, ref localLast, current);
            }
        }

        return localFirst;
    }

    /// <summary>
    /// Performs unconditional transition.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    public void Signal()
    {
        ThrowIfDisposed();

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Performs unconditional transition.
    /// </summary>
    /// <param name="transition">The transition action.</param>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    public void Signal(Action<TState> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        ThrowIfDisposed();

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            transition(State);
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Performs unconditional transition.
    /// </summary>
    /// <typeparam name="T">The type of the argument to be passed to the transition.</typeparam>
    /// <param name="transition">The transition action.</param>
    /// <param name="arg">The argument to be passed to the transition.</param>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    public void Signal<T>(Action<TState, T> transition, T arg)
    {
        ArgumentNullException.ThrowIfNull(transition);

        ThrowIfDisposed();
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            transition(State, arg);
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }

        suspendedCallers?.Unwind();
    }

    /// <summary>
    /// Performs conditional transition synchronously.
    /// </summary>
    /// <param name="transition">The condition to be examined immediately.</param>
    /// <returns>The result of <see cref="ITransition.Test(TState)"/> invocation.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    public bool TrySignal(ITransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        ThrowIfDisposed();
        lock (SyncRoot)
        {
            var manager = new LockManager(State, transition);
            return TryAcquire(ref manager);
        }
    }

    /// <summary>
    /// Performs conditional transition asynchronously.
    /// </summary>
    /// <param name="transition">The conditional transition.</param>
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Signal()"/>
    public ValueTask<bool> WaitAsync(ITransition transition, TimeSpan timeout, CancellationToken token = default)
    {
        var manager = new LockManager(State, transition);
        return TryAcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
    }

    /// <summary>
    /// Suspends the caller and waits for the signal.
    /// </summary>
    /// <param name="transition">The conditional transition.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Signal()"/>
    public ValueTask WaitAsync(ITransition transition, CancellationToken token = default)
    {
        var manager = new LockManager(State, transition);
        return AcquireAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }

    private protected sealed override bool IsReadyToDispose => first is null;
}