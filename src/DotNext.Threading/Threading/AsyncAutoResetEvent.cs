using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Represents asynchronous version of <see cref="AutoResetEvent"/>.
/// </summary>
[DebuggerDisplay($"IsSet = {{{nameof(IsSet)}}}")]
public class AsyncAutoResetEvent : QueuedSynchronizer, IAsyncResetEvent
{
    [StructLayout(LayoutKind.Auto)]
    private struct StateManager : ILockManager, IConsumer<WaitNode>
    {
        internal bool Value;

        internal StateManager(bool initialState)
            => Value = initialState;

        readonly bool ILockManager.IsLockAllowed => Value;

        void ILockManager.AcquireLock() => Value = false;

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;
    }

    private StateManager manager;

    /// <summary>
    /// Initializes a new asynchronous reset event in the specified state.
    /// </summary>
    /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
    public AsyncAutoResetEvent(bool initialState)
    {
        manager = new(initialState);
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => waitQueueVisitor.SignalAll(ref manager);

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => Volatile.Read(in manager.Value);

    /// <summary>
    /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = TryAcquire(ref manager);
        Monitor.Exit(SyncRoot);

        return result;
    }

    /// <summary>
    /// Sets the state of the event to signaled, resuming the suspended caller.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Set()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool result;

        if (manager.Value)
        {
            result = false;
        }
        else
        {
            ManualResetCompletionSource? suspendedCaller;
            lock (SyncRoot)
            {
                if (manager.Value)
                {
                    suspendedCaller = null;
                    result = false;
                }
                else
                {
                    manager.Value = result = true;
                    suspendedCaller = DrainWaitQueue();
                }
            }

            suspendedCaller?.NotifyConsumer();
        }

        return result;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Signal() => Set();

    /// <inheritdoc/>
    EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.AutoReset;

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
        => TryAcquireAsync<WaitNode, StateManager>(ref manager, timeout, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync<WaitNode, StateManager>(ref manager, token);
}