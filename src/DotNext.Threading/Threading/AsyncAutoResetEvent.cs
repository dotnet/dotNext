using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;
using Tasks;

/// <summary>
/// Represents asynchronous version of <see cref="AutoResetEvent"/>.
/// </summary>
[DebuggerDisplay($"IsSet = {{{nameof(IsSet)}}}")]
public class AsyncAutoResetEvent : QueuedSynchronizer, IAsyncResetEvent
{
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct StateManager(ref bool signaled) : ILockManager<bool, StateManager>, IConsumer<WaitNode>
    {
        private readonly ref bool signaled = ref signaled;

        static StateManager ILockManager<bool, StateManager>.Create(ref bool state) => new(ref state);

        bool ILockManager.IsLockAllowed => signaled;

        void ILockManager.AcquireLock() => signaled = false;

        void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }

    private bool signaled;

    /// <summary>
    /// Initializes a new asynchronous reset event in the specified state.
    /// </summary>
    /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
    public AsyncAutoResetEvent(bool initialState)
        => signaled = initialState;

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => waitQueueVisitor.SignalAll(new StateManager(ref signaled));

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => Volatile.Read(in signaled);

    /// <summary>
    /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        TryAcquire<bool, StateManager>(ref signaled, out var result).Dispose();
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

        if (signaled)
        {
            result = false;
        }
        else
        {
            ManualResetCompletionSource? suspendedCaller;
            using (AcquireInternalLock())
            {
                if (signaled)
                {
                    suspendedCaller = null;
                    result = false;
                }
                else
                {
                    signaled = result = true;
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
        => TryAcquireAsync<WaitNode, StateManager>(new(ref signaled), timeout, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync<WaitNode, StateManager>(new(ref signaled), token);
}