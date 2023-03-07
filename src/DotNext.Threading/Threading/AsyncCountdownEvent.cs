using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        bool ILockManager.IsLockAllowed => Current is 0L;

        internal void Increment(long value) => Current = checked(Current + value);

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

    internal bool TryAddCount(long signalCount, bool autoReset)
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            if (signalCount < 0)
                throw new ArgumentOutOfRangeException(nameof(signalCount));

            if (manager.Current is 0L && !autoReset)
                return false;

            manager.Increment(signalCount);
            return true;
        }
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
    public bool Reset(long count)
    {
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        bool result;
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            // in signaled state
            if (manager.Current is not 0L)
            {
                result = false;
            }
            else
            {
                manager.Current = manager.Initial = count;
                result = true;
            }
        }

        return result;
    }

    private bool SignalCore(long signalCount, out LinkedValueTaskCompletionSource<bool>? head)
    {
        lock (SyncRoot)
        {
            if (manager.Current is 0L)
                throw new InvalidOperationException();

            bool result;
            head = (result = manager.Decrement(signalCount))
                ? DetachWaitQueue()
                : null;

            return result;
        }
    }

    private bool SignalAndResetCore(out LinkedValueTaskCompletionSource<bool>? head)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (manager.Decrement())
        {
            manager.Current = manager.Initial;
            head = DetachWaitQueue();
            return true;
        }

        head = null;
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
                LinkedValueTaskCompletionSource<bool>? queue;
                completedSynchronously = true;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        goto exit;
                    }

                    SignalAndResetCore(out queue);
                }

                ResumeAll(queue);
                task = new(false);
                break;
            default:
                if (token.IsCancellationRequested)
                {
                    task = ValueTask.FromCanceled<bool>(token);
                    goto exit;
                }

                ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposingOrDisposed)
                    {
                        task = new(GetDisposedTask<bool>());
                        goto exit;
                    }

                    if (SignalAndResetCore(out queue))
                    {
                        task = new(true);
                        goto resume_callers;
                    }

                    factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: false);
                }

                completedSynchronously = false;
                task = factory.Invoke(timeout, token);
                break;

            resume_callers:
                ResumeAll(queue);
                break;
        }

    exit:
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
        LinkedValueTaskCompletionSource<bool>? queue;
        lock (SyncRoot)
        {
            if (IsDisposingOrDisposed)
            {
                task = new(DisposedTask);
                goto exit;
            }

            if (SignalAndResetCore(out queue))
            {
                task = ValueTask.CompletedTask;
                goto resume_callers;
            }

            factory = EnqueueNode(ref pool, ref manager, throwOnTimeout: true);
        }

        completedSynchronously = false;
        task = factory.Invoke(token);
        goto exit;

    resume_callers:
        ResumeAll(queue);

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
        if (result = SignalCore(signalCount, out var head))
            ResumeAll(head);

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