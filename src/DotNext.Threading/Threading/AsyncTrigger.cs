using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;
using LinkedValueTaskCompletionSource = Tasks.LinkedValueTaskCompletionSource<bool>;

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

    private ValueTaskPool<bool, DefaultWaitNode, Action<DefaultWaitNode>> pool;
    private LockManager manager;

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

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(DefaultWaitNode node)
    {
        if (node.NeedsRemoval)
            RemoveNode(node);

        pool.Return(node);
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Reset() => false;

    private bool SignalCore()
    {
        Debug.Assert(Monitor.IsEntered(this));

        for (LinkedValueTaskCompletionSource? current = first, next; current is not null; current = next)
        {
            next = current.Next;

            if (RemoveAndSignal(current))
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private bool Signal() => SignalCore();

    [MethodImpl(MethodImplOptions.Synchronized)]
    private new LinkedValueTaskCompletionSource<bool>? DetachWaitQueue() => base.DetachWaitQueue();

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
        return resumeAll ? ResumeAll(DetachWaitQueue()) > 0L : Signal();
    }

    private bool SignalCore(bool resumeAll)
    {
        Debug.Assert(Monitor.IsEntered(this));

        return resumeAll ? ResumeAll(base.DetachWaitQueue()) > 0L : SignalCore();
    }

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => first is null;

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Signal();

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory Wait(bool zeroTimeout)
        => Wait(ref manager, ref pool, throwOnTimeout: false, zeroTimeout);

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
        if (ValidateTimeoutAndToken(timeout, token, out ValueTask<bool> task))
            task = Wait(timeout == TimeSpan.Zero).CreateTask(timeout, token);

        return task;
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
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : Wait(zeroTimeout: false).CreateVoidTask(token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory SignalAndWait(bool resumeAll, bool throwOnEmptyQueue, bool zeroTimeout)
    {
        ValueTaskFactory factory;

        if (IsDisposingOrDisposed)
        {
            factory = new(GetDisposedTask<bool>());
        }
        else if (!SignalCore(resumeAll) && throwOnEmptyQueue)
        {
            factory = new(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue));
        }
        else
        {
            factory = Wait(ref manager, ref pool, throwOnTimeout: false, zeroTimeout);
        }

        return factory;
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
    /// <param name="timeout">The time to wait for the signal.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
    public ValueTask<bool> SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
    {
        if (ValidateTimeoutAndToken(timeout, token, out ValueTask<bool> task))
            task = SignalAndWait(resumeAll, throwOnEmptyQueue, timeout == TimeSpan.Zero).CreateTask(timeout, token);

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
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : SignalAndWait(resumeAll, throwOnEmptyQueue, zeroTimeout: false).CreateVoidTask(token);

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
    {
        ValueTask<bool> task;
        if (ValidateTimeoutAndToken(timeout, token, out task))
            task = SpinWaitCoreAsync(condition, new(timeout), token);

        return task;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> SpinWaitCoreAsync<TCondition>(TCondition condition, Timeout timeout, CancellationToken token)
        where TCondition : notnull, ISupplier<bool>
    {
        do
        {
            if (condition.Invoke())
                return true;
        }
        while (timeout.RemainingTime.TryGetValue(out var remainingTime) && await Wait(remainingTime == TimeSpan.Zero).CreateTask(remainingTime, token).ConfigureAwait(false));

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
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : SpinWaitCoreAsync(condition, token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SpinWaitCoreAsync<TCondition>(TCondition condition, CancellationToken token)
        where TCondition : notnull, ISupplier<bool>
    {
        while (!condition.Invoke())
            await Wait(zeroTimeout: false).CreateVoidTask(token).ConfigureAwait(false);
    }
}

/// <summary>
/// Represents asynchronous trigger that allows to resume and suspend
/// concurrent flows.
/// </summary>
/// <typeparam name="TState">The type of the state used for coordination.</typeparam>
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

        private protected override void ResetCore()
        {
            Transition = null;
            base.ResetCore();
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

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(WaitNode node)
    {
        if (node.NeedsRemoval && RemoveNode(node))
            DrainWaitQueue();

        pool.Return(node);
    }

    /// <summary>
    /// Gets state of this trigger.
    /// </summary>
    public TState State { get; }

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(first is null or WaitNode);

        for (WaitNode? current = Unsafe.As<WaitNode>(first), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or WaitNode);

            next = Unsafe.As<WaitNode>(current.Next);

            var transition = current.Transition;

            if (current.IsCompleted || transition is null)
            {
                RemoveNode(current);
                continue;
            }

            if (!transition.Test(State))
                break;

            if (RemoveAndSignal(current))
                transition.Transit(State);
        }
    }

    /// <summary>
    /// Performs unconditional transition.
    /// </summary>
    /// <param name="transition">The transition action.</param>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Signal(Action<TState> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ThrowIfDisposed();
        transition(State);
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    /// <summary>
    /// Performs unconditional transition.
    /// </summary>
    /// <typeparam name="T">The type of the argument to be passed to the transition.</typeparam>
    /// <param name="transition">The transition action.</param>
    /// <param name="arg">The argument to be passed to the transition.</param>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Signal<T>(Action<TState, T> transition, T arg)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ThrowIfDisposed();
        transition(State, arg);
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    /// <summary>
    /// Performs conditional transition synchronously.
    /// </summary>
    /// <param name="transition">The condition to be examined immediately.</param>
    /// <returns>The result of <see cref="ITransition.Test(TState)"/> invocation.</returns>
    /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TrySignal(ITransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ThrowIfDisposed();

        var manager = new LockManager(State, transition);
        return TryAcquire(ref manager);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory Wait(ref LockManager manager, bool zeroTimeout)
        => Wait(ref manager, ref pool, throwOnTimeout: false, zeroTimeout);

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
    /// <seealso cref="Signal"/>
    public ValueTask<bool> WaitAsync(ITransition transition, TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask<bool> task;

        if (transition is null)
        {
            task = ValueTask.FromException<bool>(new ArgumentNullException(nameof(transition)));
        }
        else if (ValidateTimeoutAndToken(timeout, token, out task))
        {
            var manager = new LockManager(State, transition);
            task = Wait(ref manager, timeout == TimeSpan.Zero).CreateTask(timeout, token);
        }

        return task;
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
    /// <seealso cref="Signal"/>
    public ValueTask WaitAsync(ITransition transition, CancellationToken token = default)
    {
        ValueTask task;

        if (transition is null)
        {
            task = ValueTask.FromException(new ArgumentNullException(nameof(transition)));
        }
        else if (token.IsCancellationRequested)
        {
            task = ValueTask.FromCanceled(token);
        }
        else
        {
            var manager = new LockManager(State, transition);
            task = Wait(ref manager, zeroTimeout: false).CreateVoidTask(token);
        }

        return task;
    }

    private protected sealed override bool IsReadyToDispose => first is null;
}