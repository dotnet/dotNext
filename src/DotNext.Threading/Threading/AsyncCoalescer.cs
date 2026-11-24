using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Coalesces concurrent callers into a single invocation of the asynchronous action.
/// </summary>
/// <remarks>
/// The first caller of <see cref="WaitAsync(CancellationToken)"/> opens the time window.
/// The callers arriving within the window are suspended and join the same batch.
/// When the window expires, or <see cref="Flush"/> is called, the window is closed, and the action
/// is invoked once for all the callers of the batch. The suspended callers are resumed when the action completes.
/// <para>
/// The action always starts after all the callers of the batch have joined it. Therefore, the completion
/// of the action observed by the caller is caused by the invocation started after the call to
/// <see cref="WaitAsync(CancellationToken)"/>. The callers arriving while the action is in progress
/// open the next window and never observe the completion of the action started before them.
/// </para>
/// <para>
/// The window is fixed: it's not extended by the callers arriving within it. The next window is opened
/// by the first caller after the closure of the previous window, so the actions of the consecutive windows
/// can run concurrently.
/// </para>
/// <para>
/// The action runs on the thread pool without the <see cref="ExecutionContext"/> of the callers.
/// </para>
/// </remarks>
public abstract partial class AsyncCoalescer : IAsyncDisposable
{
    private readonly System.Threading.Lock syncRoot = new();
    private readonly TimeSpan delay;
    private readonly ITimer timer;
    private ValueTaskPool<bool> pool;
    private bool disposed;

    /// <summary>
    /// Initializes a new coalescer.
    /// </summary>
    /// <param name="delay">
    /// The duration of the time window, measured from the arrival of its first caller.
    /// <see cref="InfiniteTimeSpan"/> means that the window is closed only by <see cref="Flush"/>.
    /// </param>
    /// <param name="provider">The time provider used to create the timer of the window.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative and not <see cref="InfiniteTimeSpan"/>.</exception>
    protected AsyncCoalescer(TimeSpan delay, TimeProvider provider)
    {
        Timeout.Validate(delay);

        this.delay = delay;
        pool = new();
        timer = provider.CreateTimer(OnTimeout, this, InfiniteTimeSpan, InfiniteTimeSpan);

        static void OnTimeout(object? state)
        {
            Debug.Assert(state is AsyncCoalescer);

            Unsafe.As<AsyncCoalescer>(state).Complete(triggeredManually: false);
        }
    }

    /// <summary>
    /// Gets or sets the maximum expected number of suspender callers in a batch.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not <see langword="null"/> and less than 1.</exception>
    public long ConcurrencyLevel
    {
        get => pool.MaximumRetained;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            pool = new(value);
        }
    }

    /// <summary>
    /// Performs the action for all the callers of the closed window.
    /// </summary>
    /// <remarks>
    /// The method is called on the thread pool. Multiple invocations can run concurrently,
    /// because the next window can be closed while the action of the previous window is still in progress.
    /// The exception thrown by the method is passed to all the callers of the window.
    /// </remarks>
    /// <param name="triggeredManually">
    /// <see langword="true"/> if the window is closed by <see cref="Flush"/>;
    /// <see langword="false"/> if the window is expired.
    /// </param>
    /// <returns>The task representing asynchronous execution of the action.</returns>
    protected abstract Task CompleteAsync(bool triggeredManually);

    /// <summary>
    /// Suspends the caller until the action is completed for the window that the caller joins.
    /// </summary>
    /// <remarks>
    /// The caller opens a new window if there is no open window. The action invoked for the window starts
    /// after this method is called.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the waiting.</param>
    /// <returns>
    /// <see langword="true"/> if the window has been closed by <see cref="Flush"/>;
    /// <see langword="false"/> if the window has been expired.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The coalescer has been disposed.</exception>
    /// <exception cref="Exception">The exception thrown by <see cref="CompleteAsync(bool)"/>.</exception>
    public ValueTask<bool> WaitAsync(CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return ValueTask.FromCanceled<bool>(token);

        WaitNode node;
        lock (syncRoot)
        {
            if (disposed)
                return ValueTask.FromException<bool>(CreateDisposedException());

            if (current is null)
            {
                // the first caller opens the window
                current = RentBatch();
                timer.Change(delay, InfiniteTimeSpan);
            }

            node = pool.Rent<WaitNode>();
            node.Initialize(this, current);
            current.Add(node);
        }

        return node.Invoke<ValueTask<bool>>(token);
    }

    /// <summary>
    /// Closes the open window immediately, without waiting for its expiration, and starts the action.
    /// </summary>
    /// <remarks>
    /// The method doesn't wait for the action to complete. It does nothing if there is no open window,
    /// or the coalescer has been disposed.
    /// </remarks>
    public void Flush() => Complete(triggeredManually: true);

    private void Complete(bool triggeredManually)
    {
        Batch? batch;
        lock (syncRoot)
        {
            // Nothing to do if the window is not open, or it's already closed by the concurrent trigger.
            // The stale timer callback may close the next window earlier than expected. It's safe, because
            // the action still starts after all the callers of the batch have joined it
            batch = current;
            if (batch is null)
                return;

            // Close the window BEFORE the action starts, so the callers arriving later
            // join the new window and never observe the completion of the action started before them
            current = null;

            if (triggeredManually)
                timer.Change(InfiniteTimeSpan, InfiniteTimeSpan);
        }

        // Moves the action out of the timer thread
        batch.Start(this, triggeredManually);
    }

    private ObjectDisposedException CreateDisposedException()
    {
        var e = new ObjectDisposedException(GetType().Name);
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        return e;
    }

    private void ReturnNode(WaitNode node)
    {
        if (node.NeedsRemoval)
        {
            lock (syncRoot)
            {
                // The canceled node must be removed from its own batch, which is not necessarily the batch
                // of the open window. If the batch is already drained, the removal is no-op
                node.RemoveFromBatch();
            }
        }

        node.Reset();
        if (!disposed)
        {
            lock (syncRoot)
            {
                if (!disposed)
                    pool.Return(node);
            }
        }
    }

    /// <summary>
    /// Disposes the coalescer.
    /// </summary>
    /// <remarks>
    /// The callers of the open window are resumed with <see cref="ObjectDisposedException"/>.
    /// The actions in progress are not interrupted, and this method doesn't wait for them:
    /// they resume the callers of their windows with their own results.
    /// </remarks>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public ValueTask DisposeAsync()
    {
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (syncRoot)
        {
            if (disposed)
                return ValueTask.CompletedTask;

            disposed = true;
            suspendedCallers = current?.Drain(new Result<bool>(CreateDisposedException()));
            current = null;
            batchPool = null;
            batchPoolSize = 0;
            pool.Reset();
        }

        suspendedCallers?.Unwind();
        return timer.DisposeAsync();
    }

    private sealed class WaitNode : LinkedValueTaskCompletionSource<bool>
    {
        private AsyncCoalescer? owner;
        private Batch? batch;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;

        internal void Initialize(AsyncCoalescer owner, Batch batch)
        {
            this.owner = owner;
            this.batch = batch;
        }

        internal void RemoveFromBatch() => batch?.Remove(this);

        protected override void CleanUp()
        {
            owner = null;
            batch = null;
            base.CleanUp();
        }

        protected override void AfterConsumed() => owner?.ReturnNode(this);
    }
}
