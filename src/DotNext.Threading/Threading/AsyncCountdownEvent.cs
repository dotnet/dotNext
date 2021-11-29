using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

using Tasks.Pooling;

/// <summary>
/// Represents a synchronization primitive that is signaled when its count reaches zero.
/// </summary>
/// <remarks>
/// This is asynchronous version of <see cref="System.Threading.CountdownEvent"/>.
/// </remarks>
public class AsyncCountdownEvent : QueuedSynchronizer, IAsyncEvent
{
    [StructLayout(LayoutKind.Auto)]
    private struct StateManager : ILockManager<DefaultWaitNode>
    {
        private long current, initial;

        internal StateManager(long initialCount)
            => current = initial = initialCount;

        internal long Current
        {
            readonly get => current.VolatileRead();
            set => current.VolatileWrite(value);
        }

        internal readonly bool IsEmpty => Current == 0L;

        bool ILockManager.IsLockAllowed => IsEmpty;

        internal long Initial
        {
            readonly get => initial.VolatileRead();
            set => initial.VolatileWrite(value);
        }

        internal void Increment(long value) => current.AddAndGet(value);

        internal unsafe bool Decrement(long value)
        {
            return current.AccumulateAndGet(value, &Accumulate) == 0L;

            static long Accumulate(long current, long value)
                => Math.Max(0L, current - value);
        }

        void ILockManager.AcquireLock()
        {
            // nothing to do here
        }

        void ILockManager<DefaultWaitNode>.InitializeNode(DefaultWaitNode node)
        {
            // nothing to do here
        }
    }

    private ValueTaskPool<bool, DefaultWaitNode> pool;
    private StateManager manager;

    /// <summary>
    /// Creates a new countdown event with the specified count.
    /// </summary>
    /// <param name="initialCount">The number of signals initially required to set the event.</param>
    /// <param name="concurrencyLevel">The expected number of suspended callers.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="initialCount"/> is less than zero;
    /// or <paramref name="concurrencyLevel"/> is less than or equal to zero.
    /// </exception>
    public AsyncCountdownEvent(long initialCount, int concurrencyLevel)
    {
        if (initialCount < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCount));
        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        manager = new(initialCount);
        pool = new(OnCompleted, concurrencyLevel);
    }

    /// <summary>
    /// Creates a new countdown event with the specified count.
    /// </summary>
    /// <param name="initialCount">The number of signals initially required to set the event.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCount"/> is less than zero.</exception>
    public AsyncCountdownEvent(long initialCount)
    {
        if (initialCount < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCount));

        manager = new(initialCount);
        pool = new(OnCompleted);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void OnCompleted(DefaultWaitNode node)
    {
        RemoveAndDrainWaitQueue(node);
        pool.Return(node);
    }

    /// <summary>
    /// Gets the numbers of signals initially required to set the event.
    /// </summary>
    public long InitialCount => manager.Initial;

    /// <summary>
    /// Gets the number of remaining signals required to set the event.
    /// </summary>
    public long CurrentCount => manager.Current;

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => manager.IsEmpty;

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal bool TryAddCount(long signalCount, bool autoReset)
    {
        ThrowIfDisposed();

        if (signalCount < 0)
            throw new ArgumentOutOfRangeException(nameof(signalCount));

        if (manager.IsEmpty && !autoReset)
            return false;

        manager.Increment(signalCount);
        return true;
    }

    /// <summary>
    /// Attempts to increment the current count by a specified value.
    /// </summary>
    /// <param name="signalCount">The value by which to increase <see cref="CurrentCount"/>.</param>
    /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than zero.</exception>
    public bool TryAddCount(long signalCount) => TryAddCount(signalCount, false);

    /// <summary>
    /// Attempts to increment the current count by one.
    /// </summary>
    /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool TryAddCount() => TryAddCount(1L);

    /// <summary>
    /// Increments the current count by a specified value.
    /// </summary>
    /// <param name="signalCount">The value by which to increase <see cref="CurrentCount"/>.</param>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than zero.</exception>
    /// <exception cref="InvalidOperationException">The current instance is already set.</exception>
    public void AddCount(long signalCount)
    {
        if (!TryAddCount(signalCount))
            throw new InvalidOperationException();
    }

    /// <summary>
    /// Increments the current count by one.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="InvalidOperationException">The current instance is already set.</exception>
    public void AddCount() => AddCount(1L);

    /// <summary>
    /// Resets the <see cref="CurrentCount"/> to the value of <see cref="InitialCount"/>.
    /// </summary>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset() => Reset(manager.Initial);

    /// <summary>
    /// Resets the <see cref="InitialCount"/> property to a specified value.
    /// </summary>
    /// <param name="count">The number of signals required to set this event.</param>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool Reset(long count)
    {
        ThrowIfDisposed();
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        // in signaled state
        if (!manager.IsEmpty)
            return false;

        manager.Current = manager.Initial = count;
        return true;
    }

    private bool SignalCore(long signalCount)
    {
        Debug.Assert(Monitor.IsEntered(this));

        if (manager.IsEmpty)
            throw new InvalidOperationException();

        if (manager.Decrement(signalCount))
        {
            ResumeSuspendedCallers();
            return true;
        }

        return false;
    }

    private bool SignalAndResetCore(long signalCount)
    {
        Debug.Assert(Monitor.IsEntered(this));

        if (manager.Decrement(signalCount))
        {
            ResumeSuspendedCallers();
            manager.Current = manager.Initial;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal ValueTask<bool> SignalAndWaitAsync(out bool completedSynchronously, TimeSpan timeout, CancellationToken token)
    {
        if (IsDisposed || IsDisposeRequested)
        {
            completedSynchronously = true;
            return new(GetDisposedTask<bool>());
        }

        return (completedSynchronously = SignalAndResetCore(1L)) ? new(true) : WaitNoTimeoutAsync(ref manager, ref pool, timeout, token);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal ValueTask SignalAndWaitAsync(out bool completedSynchronously, CancellationToken token)
    {
        if (IsDisposed || IsDisposeRequested)
        {
            completedSynchronously = true;
            return new(DisposedTask);
        }

        return (completedSynchronously = SignalAndResetCore(1L)) ? ValueTask.CompletedTask : WaitWithTimeoutAsync(ref manager, ref pool, InfiniteTimeSpan, token);
    }

    /// <summary>
    /// Registers multiple signals with this object, decrementing the value of <see cref="CurrentCount"/> by the specified amount.
    /// </summary>
    /// <param name="signalCount">The number of signals to register.</param>
    /// <returns><see langword="true"/> if the signals caused the count to reach zero and the event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The current instance is already set; or <paramref name="signalCount"/> is greater than <see cref="CurrentCount"/>.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool Signal(long signalCount = 1L)
    {
        if (signalCount < 1L)
            throw new ArgumentOutOfRangeException(nameof(signalCount));

        ThrowIfDisposed();

        return SignalCore(signalCount);
    }

    /// <inheritdoc />
    bool IAsyncEvent.Signal() => Signal();

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="timeout">The interval to wait for the signaled state.</param>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        => WaitNoTimeoutAsync(ref manager, ref pool, timeout, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ValueTask WaitAsync(CancellationToken token = default)
        => WaitWithTimeoutAsync(ref manager, ref pool, InfiniteTimeSpan, token);
}
