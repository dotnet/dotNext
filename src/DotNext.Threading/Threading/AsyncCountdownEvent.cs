using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

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
    private struct StateManager : ILockManager<DefaultWaitNode>
    {
        internal long Current, Initial;

        internal StateManager(long initialCount)
            => Current = Initial = initialCount;

        readonly bool ILockManager.IsLockAllowed => Current is 0L;

        internal void Increment(long value) => Current = checked(Current + value);

        internal void IncrementInitial(long value)
            => Current = Initial = checked(Current + value);

        internal bool Decrement(long value = 1L)
            => (Current = Math.Max(0L, Current - value)) is 0L;

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

    private void OnCompleted(DefaultWaitNode node)
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                RemoveNode(node);

            pool.Return(node);
        }
    }

    /// <summary>
    /// Gets the numbers of signals initially required to set the event.
    /// </summary>
    public long InitialCount => manager.Initial.VolatileRead();

    /// <summary>
    /// Gets the number of remaining signals required to set the event.
    /// </summary>
    public long CurrentCount => manager.Current.VolatileRead();

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
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public bool Reset()
    {
        ThrowIfDisposed();
        return ResetCore(1L);
    }

    /// <summary>
    /// Resets the <see cref="InitialCount"/> property to a specified value.
    /// </summary>
    /// <param name="count">The number of signals required to set this event.</param>
    /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    public bool Reset(long count)
    {
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        ThrowIfDisposed();
        return ResetCore(count);
    }

    private bool ResetCore(long count)
    {
        Debug.Assert(count >= 0L);

        bool result;
        Monitor.Enter(SyncRoot);

        // the following code never throws, avoid try-finally overhead
        result = manager.Current is 0L;
        manager.Current = manager.Initial = count;

        Monitor.Exit(SyncRoot);
        return result;
    }

    private bool SignalAndResetCore(out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (manager.Decrement())
        {
            manager.Current = manager.Initial;
            suspendedCallers = DetachWaitQueue()?.SetResult(true, out _);
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
            case < 0L:
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

                    factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: false);
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

            factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: true);
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

        ThrowIfDisposed();

        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (manager.Current is 0L)
                throw new InvalidOperationException();

            suspendedCallers = (result = manager.Decrement(signalCount))
                ? DetachWaitQueue()?.SetResult(true, out _)
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