using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Represents a synchronization primitive that is signaled when its count reaches zero.
/// </summary>
/// <remarks>
/// This is asynchronous version of <see cref="System.Threading.CountdownEvent"/>.
/// </remarks>
[DebuggerDisplay($"Counter = {{{nameof(CurrentCount)}}}")]
public class AsyncCountdownEvent : QueuedSynchronizer, IAsyncEvent
{
    [StructLayout(LayoutKind.Auto)]
    private struct StateManager : ILockManager, IConsumer<WaitNode>
    {
        internal long Current, Initial;

        internal StateManager(long initialCount)
            => Current = Initial = initialCount;

        public readonly bool IsLockAllowed => Current is 0L;

        internal void Increment(long value) => Current = checked(Current + value);

        internal void IncrementInitial(long value)
            => Current = Initial = checked(Current + value);

        internal void Decrement(long value = 1L)
            => Current = Math.Max(0L, Current - value);

        readonly void ILockManager.AcquireLock()
        {
            // nothing to do here
        }

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node) => node.DrainOnReturn = false;
    }

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
        : base(concurrencyLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);

        manager = new(initialCount);
    }

    /// <summary>
    /// Creates a new countdown event with the specified count.
    /// </summary>
    /// <param name="initialCount">The number of signals initially required to set the event.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCount"/> is less than zero.</exception>
    public AsyncCountdownEvent(long initialCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);

        manager = new(initialCount);
    }

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor) => waitQueueVisitor.SignalAll();

    /// <summary>
    /// Gets the numbers of signals initially required to set the event.
    /// </summary>
    public long InitialCount => Atomic.Read(in manager.Initial);

    /// <summary>
    /// Gets the number of remaining signals required to set the event.
    /// </summary>
    public long CurrentCount => Atomic.Read(in manager.Current);

    /// <summary>
    /// Indicates whether this event is set.
    /// </summary>
    public bool IsSet => CurrentCount is 0L;

    internal void AddCountAndReset(long signalCount)
    {
        Debug.Assert(signalCount > 0L);

        Monitor.Enter(SyncRoot);
        manager.IncrementInitial(signalCount);
        Monitor.Exit(SyncRoot);
    }

    private bool TryAddCountCore(long signalCount)
    {
        Debug.Assert(signalCount > 0L);

        bool result;
        Monitor.Enter(SyncRoot);

        if (result = manager.Current is not 0L)
        {
            manager.Increment(signalCount);
        }

        Monitor.Exit(SyncRoot);
        return result;
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
        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = manager.Current is 0L;
            manager.Current = manager.Initial;
            suspendedCallers = Interrupt();
        }

        suspendedCallers?.Unwind();
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

        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = manager.Current is 0L;
            manager.Current = manager.Initial = count;
            suspendedCallers = Interrupt();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    private bool SignalAndResetCore(out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        manager.Decrement();
        if (manager.IsLockAllowed)
        {
            manager.Current = manager.Initial;
            suspendedCallers = DrainWaitQueue();
            return true;
        }

        suspendedCallers = null;
        return false;
    }

    internal ValueTask<bool> SignalAndWaitAsync(out bool completedSynchronously, TimeSpan timeout, CancellationToken token)
    {
        ValueTask<bool> task;
        completedSynchronously = true;

        switch (timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
                task = ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                LinkedValueTaskCompletionSource<bool>? suspendedCallers;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    SignalAndResetCore(out suspendedCallers);
                }

                suspendedCallers?.Unwind();
                task = new(false);
                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(token);
                    break;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        break;
                    }

                    if (SignalAndResetCore(out suspendedCallers))
                    {
                        task = new(true);
                        goto resume_suspended_callers;
                    }

                    factory = EnqueueNode();
                }

                completedSynchronously = false;
                task = factory.Invoke(timeout, token);
                break;

            resume_suspended_callers:
                suspendedCallers?.Unwind();
                break;
        }

        return task;
    }

    internal ValueTask SignalAndWaitAsync(out bool completedSynchronously, CancellationToken token)
    {
        ValueTask task;
        completedSynchronously = true;

        if (token.IsCancellationRequested)
        {
            task = ValueTask.FromCanceled(token);
            goto exit;
        }

        ISupplier<TimeSpan, CancellationToken, ValueTask> factory;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (IsDisposingOrDisposed)
            {
                task = new(DisposedTask);
                goto exit;
            }

            if (SignalAndResetCore(out suspendedCallers))
            {
                task = ValueTask.CompletedTask;
                goto resume_suspended_callers;
            }

            factory = EnqueueNodeThrowOnTimeout();
        }

        completedSynchronously = false;
        task = factory.Invoke(token);
        goto exit;

    resume_suspended_callers:
        suspendedCallers?.Unwind();

    exit:
        return task;
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
        if (signalCount < 1L)
            throw new ArgumentOutOfRangeException(nameof(signalCount));

        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (manager.Current is 0L)
                throw new InvalidOperationException();

            manager.Decrement(signalCount);
            suspendedCallers = (result = manager.IsLockAllowed)
                ? DrainWaitQueue()
                : null;
        }

        suspendedCallers?.Unwind();
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
        => TryAcquireAsync<WaitNode, StateManager, TimeoutAndCancellationToken>(ref manager, new(timeout, token));

    /// <summary>
    /// Turns caller into idle state until the current event is set.
    /// </summary>
    /// <param name="token">The token that can be used to abort wait process.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync<WaitNode, StateManager, CancellationTokenOnly>(ref manager, new CancellationTokenOnly(token));
}