using System.Runtime.CompilerServices;
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
    private struct State
    {
        private long current, initial;

        internal State(long initialCount)
            => current = initial = initialCount;

        internal long Current
        {
            readonly get => current.VolatileRead();
            set => current.VolatileWrite(value);
        }

        internal long Initial
        {
            readonly get => initial.VolatileRead();
            set => initial.VolatileWrite(value);
        }

        internal void Increment(long value) => current.Add(value);

        internal unsafe bool Decrement(long value)
        {
            return current.AccumulateAndGet(value, &Accumulate) == 0L;

            static long Accumulate(long current, long value)
                => Math.Max(0L, current - value);
        }
    }

    private readonly Func<DefaultWaitNode> pool;
    private State state;

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

        state = new(initialCount);
        pool = new ConstrainedValueTaskPool<DefaultWaitNode>(concurrencyLevel).Get;
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

        state = new(initialCount);
        pool = new UnconstrainedValueTaskPool<DefaultWaitNode>().Get;
    }

    private static bool IsEmpty(ref State state) => state.Current == 0L;

    /// <summary>
    /// Gets the numbers of signals initially required to set the event.
    /// </summary>
    public long InitialCount => state.Initial;

    /// <summary>
    /// Gets the number of remaining signals required to set the event.
    /// </summary>
    public long CurrentCount => state.Current;

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => IsEmpty(ref state);

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal bool TryAddCount(long signalCount, bool autoReset)
    {
        ThrowIfDisposed();

        if (signalCount < 0)
            throw new ArgumentOutOfRangeException(nameof(signalCount));

        if (IsEmpty(ref state) && !autoReset)
            return false;

        state.Increment(signalCount);
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
    public bool Reset() => Reset(state.Initial);

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
        if (!IsEmpty(ref state))
            return false;

        state.Current = state.Initial = count;
        return true;
    }

    private bool SignalCore(long signalCount)
    {
        if (IsEmpty(ref state))
            throw new InvalidOperationException();

        if (state.Decrement(signalCount))
        {
            ResumeSuspendedCallers();
            return true;
        }

        return false;
    }

    private bool SignalAndResetCore(long signalCount)
    {
        Debug.Assert(Monitor.IsEntered(this));

        if (state.Decrement(signalCount))
        {
            ResumeSuspendedCallers();
            state.Current = state.Initial;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal unsafe ValueTask<bool> SignalAndWaitAsync(out bool completedSynchronously, TimeSpan timeout, CancellationToken token)
        => (completedSynchronously = SignalAndResetCore(1L)) ? new(true) : WaitNoTimeoutAsync(ref state, &IsEmpty, pool, out _, timeout, token);

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal unsafe ValueTask SignalAndWaitAsync(out bool completedSynchronously, CancellationToken token)
        => (completedSynchronously = SignalAndResetCore(1L)) ? ValueTask.CompletedTask : WaitWithTimeoutAsync(ref state, &IsEmpty, pool, out _, InfiniteTimeSpan, token);

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
    public unsafe ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        => WaitNoTimeoutAsync(ref state, &IsEmpty, pool, out _, timeout, token);

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public unsafe ValueTask WaitAsync(CancellationToken token = default)
        => WaitWithTimeoutAsync(ref state, &IsEmpty, pool, out _, InfiniteTimeSpan, token);
}
