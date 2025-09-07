using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Represents a synchronization primitive that is signaled when its count becomes non-zero.
/// </summary>
/// <remarks>
/// This class behaves in opposite to <see cref="AsyncCountdownEvent"/>.
/// Every call of <see cref="Increment()"/> increments the counter.
/// Every call of <see cref="WaitAsync(TimeSpan, CancellationToken)"/>
/// decrements counter and release the caller if the current count is greater than zero.
/// </remarks>
[DebuggerDisplay($"Counter = {{{nameof(Value)}}}")]
public class AsyncCounter : QueuedSynchronizer, IAsyncEvent
{
    [StructLayout(LayoutKind.Auto)]
    private struct StateManager : ILockManager, IConsumer<WaitNode>
    {
        internal required long Value;

        internal void Increment(long delta) => Value = checked(Value + delta);

        internal bool TryIncrement(long maxValue)
        {
            bool result;
            if (result = Value < maxValue)
                Value += 1L;

            return result;
        }

        internal bool TryReset()
        {
            var result = Value > 0L;
            Value = 0L;
            return result;
        }

        readonly bool ILockManager.IsLockAllowed => Value > 0L;

        void ILockManager.AcquireLock() => Value--;

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;
    }

    private StateManager manager;

    /// <summary>
    /// Initializes a new asynchronous counter.
    /// </summary>
    /// <param name="initialValue">The initial value of the counter.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialValue"/> is less than zero.</exception>
    public AsyncCounter(long initialValue = 0L)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialValue, 0L);

        manager = new() { Value = initialValue };
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
        => waitQueueVisitor.SignalAll(ref manager);

    /// <inheritdoc/>
    bool IAsyncEvent.IsSet => Value > 0L;

    /// <summary>
    /// Gets the counter value.
    /// </summary>
    /// <remarks>
    /// The returned value indicates how many calls you can perform
    /// using <see cref="WaitAsync(TimeSpan, CancellationToken)"/> without
    /// blocking.
    /// </remarks>
    public long Value => Atomic.Read(in manager.Value);

    /// <inheritdoc/>
    bool IAsyncEvent.Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = manager.TryReset();
        Monitor.Exit(SyncRoot);

        return result;
    }

    /// <summary>
    /// Increments counter and resume suspended callers.
    /// </summary>
    /// <exception cref="OverflowException">Counter overflow detected.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    public void Increment()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        IncrementCore(1L);
    }

    /// <summary>
    /// Increments counter and resume suspended callers.
    /// </summary>
    /// <param name="delta">The value to be added to the counter.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delta"/> is less than zero.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    /// <exception cref="OverflowException">Counter overflow detected.</exception>
    public void Increment(long delta)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        switch (delta)
        {
            case < 0L:
                throw new ArgumentOutOfRangeException(nameof(delta));
            case 0L:
                break;
            default:
                IncrementCore(delta);
                break;
        }
    }

    /// <summary>
    /// Attempts to increment this counter.
    /// </summary>
    /// <param name="maxValue">The maximum allowed value of this counter.</param>
    /// <returns><see langword="true"/> if successfully incremented; <see langword="false"/> on overflow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative or zero.</exception>
    public bool TryIncrement(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);

        return TryIncrementCore(maxValue);
    }

    private void IncrementCore(long delta)
    {
        Debug.Assert(delta > 0L);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            manager.Increment(delta);
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
    }

    private bool TryIncrementCore(long maxValue)
    {
        Debug.Assert(maxValue > 0);

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        bool result;
        lock (SyncRoot)
        {
            result = manager.TryIncrement(maxValue);
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <inheritdoc/>
    bool IAsyncEvent.Signal()
    {
        Increment();
        return true;
    }

    /// <summary>
    /// Suspends caller if <see cref="Value"/> is zero
    /// or just decrements it.
    /// </summary>
    /// <param name="timeout">Time to wait for increment.</param>
    /// <param name="token">The token that can be used to cancel the waiting operation.</param>
    /// <returns><see langword="true"/> if counter is decremented successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync<WaitNode, StateManager, TimeoutAndCancellationToken>(ref manager, new(timeout, token));

    /// <summary>
    /// Suspends caller if <see cref="Value"/> is zero
    /// or just decrements it.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the waiting operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync<WaitNode, StateManager, CancellationTokenOnly>(ref manager, new(token));

    /// <summary>
    /// Attempts to decrement the counter synchronously.
    /// </summary>
    /// <returns><see langword="true"/> if the counter decremented successfully; <see langword="false"/> if this counter is already zero.</returns>
    public bool TryDecrement()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = TryAcquire(ref manager);
        Monitor.Exit(SyncRoot);

        return result;
    }
}