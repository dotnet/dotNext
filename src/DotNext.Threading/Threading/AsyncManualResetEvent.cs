using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents asynchronous version of <see cref="ManualResetEvent"/>.
/// </summary>
[DebuggerDisplay($"IsSet = {{{nameof(IsSet)}}}")]
public class AsyncManualResetEvent : QueuedSynchronizer, IAsyncResetEvent
{
    private struct StateManager : ILockManager<DefaultWaitNode>
    {
        private AtomicBoolean state;

        internal StateManager(bool initialState)
            => state = new(initialState);

        internal bool Value
        {
            readonly get => state.Value;
            set => state.Value = value;
        }

        internal bool TryReset() => state.TrueToFalse();

        readonly bool ILockManager.IsLockAllowed => state.Value;

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
    private StateManager manager;

    /// <summary>
    /// Initializes a new asynchronous reset event in the specified state.
    /// </summary>
    /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
    /// <param name="concurrencyLevel">The potential number of suspended callers.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncManualResetEvent(bool initialState, int concurrencyLevel)
    {
        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        manager = new(initialState);
        pool = new(OnCompleted, concurrencyLevel);
    }

    /// <summary>
    /// Initializes a new asynchronous reset event in the specified state.
    /// </summary>
    /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
    public AsyncManualResetEvent(bool initialState)
    {
        manager = new(initialState);
        pool = new(OnCompleted);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void OnCompleted(DefaultWaitNode node)
    {
        if (node.NeedsRemoval)
            RemoveNode(node);

        pool.Return(node);
    }

    /// <inheritdoc/>
    EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.ManualReset;

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => manager.Value;

    /// <summary>
    /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Set() => Set(autoReset: false);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private bool SetCore(bool autoReset, out LinkedValueTaskCompletionSource<bool>? head)
    {
        ThrowIfDisposed();

        bool result;

        result = !manager.Value;
        head = DetachWaitQueue();
        manager.Value = !autoReset;

        return result;
    }

    /// <summary>
    /// Sets the state of the event to signaled, allowing one or more awaiters to proceed;
    /// and, optionally, reverts the state of the event to initial state.
    /// </summary>
    /// <param name="autoReset"><see langword="true"/> to reset this object to non-signaled state automatically; <see langword="false"/> to leave this object in signaled state.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Set(bool autoReset)
    {
        bool result;
        if (result = SetCore(autoReset, out var head))
            ResumeAll(head);

        return result;
    }

    /// <summary>
    /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool Reset()
    {
        ThrowIfDisposed();
        return manager.TryReset();
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Set();

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory WaitNoTimeout(TimeSpan timeout, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, timeout, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="timeout">The interval to wait for the signaled state.</param>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        => WaitNoTimeout(timeout, token).Create(timeout, token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory WaitNoTimeout(CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => WaitNoTimeout(token).Create(token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory WaitNoTimeout<T>(Predicate<T> condition, T arg, TimeSpan timeout, CancellationToken token)
        => manager.Value || condition(arg) ? BooleanValueTaskFactory.True : WaitNoTimeout(ref manager, ref pool, timeout, token);

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="T">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="arg">The argument to be passed to the predicate.</param>
    /// <param name="timeout">The number of time to wait before this event is set.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> WaitAsync<T>(Predicate<T> condition, T arg, TimeSpan timeout, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return WaitNoTimeout(condition, arg, timeout, token).Create(timeout, token);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory WaitNoTimeout<T>(Predicate<T> condition, T arg, CancellationToken token)
        => manager.Value || condition(arg) ? ValueTaskFactory.Completed : WaitNoTimeout(ref manager, ref pool, token);

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="T">The type of predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="arg">The argument to be passed to the predicate.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync<T>(Predicate<T> condition, T arg, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return WaitNoTimeout(condition, arg, token).Create(token);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory WaitNoTimeout<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, TimeSpan timeout, CancellationToken token)
        => manager.Value || condition(arg1, arg2) ? BooleanValueTaskFactory.True : WaitNoTimeout(ref manager, ref pool, timeout, token);

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="T1">The type of the first predicate parameter.</typeparam>
    /// <typeparam name="T2">The type of the second predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="arg1">The first argument to be passed to the predicate.</param>
    /// <param name="arg2">The second argument to be passed to the predicate.</param>
    /// <param name="timeout">The number of time to wait before this event is set.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> WaitAsync<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, TimeSpan timeout, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return WaitNoTimeout(condition, arg1, arg2, timeout, token).Create(timeout, token);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory WaitNoTimeout<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, CancellationToken token)
        => manager.Value || condition(arg1, arg2) ? ValueTaskFactory.Completed : WaitNoTimeout(ref manager, ref pool, token);

    /// <summary>
    /// Suspends the caller until this event is set.
    /// </summary>
    /// <remarks>
    /// If given predicate returns true then caller will not be suspended.
    /// </remarks>
    /// <typeparam name="T1">The type of the first predicate parameter.</typeparam>
    /// <typeparam name="T2">The type of the second predicate parameter.</typeparam>
    /// <param name="condition">Additional condition that must be checked before suspension.</param>
    /// <param name="arg1">The first argument to be passed to the predicate.</param>
    /// <param name="arg2">The second argument to be passed to the predicate.</param>
    /// <param name="token">The token that can be used to cancel waiting operation.</param>
    /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return WaitNoTimeout(condition, arg1, arg2, token).Create(token);
    }
}