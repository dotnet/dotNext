using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents a synchronization primitive that is signaled when its count reaches zero.
/// </summary>
/// <remarks>
/// This is asynchronous version of <see cref="System.Threading.CountdownEvent"/>.
/// </remarks>
[DebuggerDisplay($"Counter = {{{nameof(CurrentCount)}}}")]
public class AsyncCountdownEvent : QueuedSynchronizer, IAsyncEvent
{
    private State state;

    /// <summary>
    /// Creates a new countdown event with the specified count.
    /// </summary>
    /// <param name="initialCount">The number of signals initially required to set the event.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCount"/> is less than zero.</exception>
    public AsyncCountdownEvent(long initialCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);

        state = new(initialCount);
    }

    /// <summary>
    /// Gets the numbers of signals initially required to set the event.
    /// </summary>
    public long InitialCount => Atomic.Read(in state.Initial);

    /// <summary>
    /// Gets the number of remaining signals required to set the event.
    /// </summary>
    public long CurrentCount => Atomic.Read(in state.Current);

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => CurrentCount is 0L;

    internal void AddCountAndReset(long signalCount)
    {
        Debug.Assert(signalCount > 0L);

        TryAcquire(new AddCountAndResetTransition(ref state, signalCount), out _).Dispose();
    }

    private bool TryAddCountCore(long signalCount)
    {
        Debug.Assert(signalCount > 0L);

        TryAcquire(new AddCountTransition(ref state, signalCount), out var acquired).Dispose();
        return acquired;
    }

    /// <summary>
    /// Attempts to increment the current count by a specified value.
    /// </summary>
    /// <param name="signalCount">The value by which to increase <see cref="CurrentCount"/>.</param>
    /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than zero.</exception>
    public bool TryAddCount(long signalCount)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return signalCount switch
        {
            < 0L => throw new ArgumentOutOfRangeException(nameof(signalCount)),
            0L => true,
            _ => TryAddCountCore(signalCount),
        };
    }

    /// <summary>
    /// Attempts to increment the current count by one.
    /// </summary>
    /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool TryAddCount()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return TryAddCountCore(1L);
    }

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
    /// <remarks>
    /// All suspended callers will be resumed with <see cref="PendingTaskInterruptedException"/> exception.
    /// </remarks>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var e = PendingTaskInterruptedException.CreateAndFillStackTrace();

        using var queue = CaptureWaitQueue();
        var result = state.Current is 0L;
        state.Current = state.Initial;
        queue.SignalAll(e);
        return result;
    }

    /// <summary>
    /// Resets the <see cref="InitialCount"/> property to a specified value.
    /// </summary>
    /// <remarks>
    /// All suspended callers will be resumed with <see cref="PendingTaskInterruptedException"/> exception.
    /// </remarks>
    /// <param name="count">The number of signals required to set this event.</param>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    public bool Reset(long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var e = PendingTaskInterruptedException.CreateAndFillStackTrace();

        using var queue = CaptureWaitQueue();
        var result = state.Current is 0L;
        state.Current = state.Initial = count;
        queue.SignalAll(e);

        return result;
    }

    private bool SignalAndResetCore(ref WaitQueueScope queue)
    {
        state.Decrement();
        if (!state.IsEmpty)
            return false;

        state.Current = state.Initial;
        queue.SignalAll();
        return true;
    }

    private T SignalAndWaitAsync<T, TBuilder>(ref TBuilder builder, out bool completedSynchronously)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, IWaitQueueProvider, allows ref struct
    {
        WaitQueueScope queue;
        if (builder.IsCompleted)
        {
            queue = default;
            completedSynchronously = true;
        }
        else
        {
            queue = builder.CaptureWaitQueue();
            completedSynchronously = Acquire<T, TBuilder, WaitNode>(ref builder, SignalAndResetCore(ref queue)) is null;
        }

        var task = builder.Build();
        queue.ResumeSuspendedCallers();
        return task;
    }

    internal ValueTask<bool> SignalAndWaitAsync(out bool completedSynchronously, TimeSpan timeout, CancellationToken token)
    {
        var builder = BeginAcquisition(timeout, token);
        return SignalAndWaitAsync<ValueTask<bool>, TimeoutAndCancellationToken>(ref builder, out completedSynchronously);
    }

    internal ValueTask SignalAndWaitAsync(out bool completedSynchronously, CancellationToken token)
    {
        var builder = BeginAcquisition(token);
        return SignalAndWaitAsync<ValueTask, CancellationTokenOnly>(ref builder, out completedSynchronously);
    }

    /// <summary>
    /// Registers multiple signals with this object, decrementing the value of <see cref="CurrentCount"/> by the specified amount.
    /// </summary>
    /// <param name="signalCount">The number of signals to register.</param>
    /// <returns><see langword="true"/> if the signals caused the count to reach zero and the event was set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The current instance is already set; or <paramref name="signalCount"/> is greater than <see cref="CurrentCount"/>.</exception>
    public bool Signal(long signalCount = 1L)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(signalCount, 1L);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        using var queue = CaptureWaitQueue();
        if (state.Current is 0L)
            throw new InvalidOperationException();

        state.Decrement(signalCount);
        var result = state.IsEmpty;
        if (result)
            queue.SignalAll();

        return result;
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
    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, StateManager>(ref builder, new(ref state));
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
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, StateManager>(ref builder, new(ref state));
    }

    [StructLayout(LayoutKind.Auto)]
    private struct State(long initialCount)
    {
        public long Current = initialCount, Initial = initialCount;

        public readonly bool IsEmpty => Current is 0L;

        internal void Decrement(long value = 1L)
            => Current = long.Max(0L, Current - value);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct StateManager(ref State state) : ILockManager<WaitNode>
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed => state.IsEmpty;
        
        void ILockManager.AcquireLock()
        {
            // nothing to do here
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct AddCountAndResetTransition(ref State state, long signalCount) : ILockManager
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed => true;

        void ILockManager.AcquireLock()
            => state.Initial = checked(state.Current += signalCount);
        
        static bool ILockManager.RequiresEmptyQueue => false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct AddCountTransition(ref State state, long signalCount) : ILockManager
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed => !state.IsEmpty;

        void ILockManager.AcquireLock()
        {
            checked
            {
                state.Current += signalCount;
            }
        }

        static bool ILockManager.RequiresEmptyQueue => false;
    }
}