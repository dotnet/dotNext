using System.Diagnostics;

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
        internal bool Value;

        internal StateManager(bool initialState)
            => Value = initialState;

        internal bool TryReset()
        {
            var result = Value;

            if (result)
                Value = false;

            return result;
        }

        readonly bool ILockManager.IsLockAllowed => Value;

        readonly void ILockManager.AcquireLock()
        {
            // nothing to do here
        }

        readonly void ILockManager<DefaultWaitNode>.InitializeNode(DefaultWaitNode node)
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
    EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.ManualReset;

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => Volatile.Read(ref manager.Value);

    /// <summary>
    /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Set() => Set(autoReset: false);

    /// <summary>
    /// Sets the state of the event to signaled, allowing one or more awaiters to proceed;
    /// and, optionally, reverts the state of the event to initial state.
    /// </summary>
    /// <param name="autoReset"><see langword="true"/> to reset this object to non-signaled state automatically; <see langword="false"/> to leave this object in signaled state.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Set(bool autoReset)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = !manager.Value;
            manager.Value = !autoReset;
            suspendedCallers = DetachWaitQueue()?.SetResult(true, out _);
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <summary>
    /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = manager.TryReset();
        Monitor.Exit(SyncRoot);

        return result;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Set();

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
        => TryAcquireAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync(ref pool, ref manager, new CancellationTokenOnly(token));
}