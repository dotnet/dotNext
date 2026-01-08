using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents asynchronous version of <see cref="ManualResetEvent"/>.
/// </summary>
[DebuggerDisplay($"IsSet = {{{nameof(IsSet)}}}")]
public class AsyncManualResetEvent : QueuedSynchronizer, IAsyncResetEvent
{
    private bool signaled;

    /// <summary>
    /// Initializes a new asynchronous reset event in the specified state.
    /// </summary>
    /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
    public AsyncManualResetEvent(bool initialState)
        => signaled = initialState;

    /// <inheritdoc/>
    EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.ManualReset;

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => Volatile.Read(in signaled);

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

        using var queue = CaptureWaitQueue();
        var result = !signaled;
        signaled = !autoReset;
        queue.SignalAll();

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

        TryAcquire(new ResetTransition(ref signaled), out var acquired).Dispose();
        return acquired;
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
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, StateManager>(ref builder, new(ref signaled));
    }

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
    {
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, StateManager>(ref builder, new(ref signaled));
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct StateManager(ref bool signaled) : ILockManager<WaitNode>
    {
        private readonly ref bool signaled = ref signaled;

        bool ILockManager.IsLockAllowed => signaled;

        void ILockManager.AcquireLock()
        {
            // nothing to do here
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ResetTransition(ref bool signaled) : ILockManager
    {
        private readonly ref bool signaled = ref signaled;

        bool ILockManager.IsLockAllowed => signaled;

        void ILockManager.AcquireLock() => signaled = false;

        static bool ILockManager.RequiresEmptyQueue => false;
    }
}