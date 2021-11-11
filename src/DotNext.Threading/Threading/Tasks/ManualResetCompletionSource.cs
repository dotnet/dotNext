using System.Runtime.CompilerServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;
using ValueTaskSourceOnCompletedFlags = System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents base class for producer of value task.
/// </summary>
public abstract class ManualResetCompletionSource : IThreadPoolWorkItem
{
    private static readonly ContextCallback ContinuationInvoker = InvokeContinuation;

    private sealed class BoxedVersion
    {
        internal readonly short Value;

        internal BoxedVersion(short value) => Value = value;

        public static implicit operator BoxedVersion(short value) => new(value);
    }

    private readonly Action<object?, CancellationToken> cancellationCallback;
    private readonly bool runContinuationsAsynchronously;
    private CancellationTokenRegistration tokenTracker, timeoutTracker;
    private CancellationTokenSource? timeoutSource;

    // task management
    private Action<object?>? continuation;
    private object? continuationState, capturedContext;
    private ExecutionContext? context;
    private protected short version;
    private bool completed;

    private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
    {
        this.runContinuationsAsynchronously = runContinuationsAsynchronously;
        version = short.MinValue;

        // cached callback to avoid further allocations
        cancellationCallback = CancellationRequested;
    }

    private protected object SyncRoot => cancellationCallback;

    private void CancellationRequested(object? expectedVersion, CancellationToken token)
    {
        Debug.Assert(expectedVersion is BoxedVersion);

        // due to concurrency, this method can be called after Reset or twice
        // that's why we need to skip the call if token doesn't match (call after Reset)
        // or completed flag is set (call twice with the same token)
        if (!completed)
        {
            lock (SyncRoot)
            {
                if (!completed && Unsafe.As<BoxedVersion>(expectedVersion).Value == version)
                {
                    if (timeoutSource?.Token == token)
                        CompleteAsTimedOut();
                    else
                        CompleteAsCanceled(token);
                }
            }
        }
    }

    private protected void StartTrackingCancellation(TimeSpan timeout, CancellationToken token)
    {
        // box current token once and only if needed
        BoxedVersion? tokenHolder = null;
        if (timeout > TimeSpan.Zero)
        {
            timeoutSource ??= new();
            tokenHolder = version;
            timeoutTracker = timeoutSource.Token.UnsafeRegister(cancellationCallback, tokenHolder);
            timeoutSource.CancelAfter(timeout);
        }

        if (token.CanBeCanceled)
        {
            tokenTracker = token.UnsafeRegister(cancellationCallback, tokenHolder ?? version);
        }
    }

    private protected abstract void CompleteAsTimedOut();

    private protected abstract void CompleteAsCanceled(CancellationToken token);

    private protected static object? CaptureContext()
    {
        object? context = SynchronizationContext.Current;
        if (context is null || context.GetType() == typeof(SynchronizationContext))
        {
            var scheduler = TaskScheduler.Current;
            context = ReferenceEquals(scheduler, TaskScheduler.Default) ? null : scheduler;
        }

        return context;
    }

    private protected void StopTrackingCancellation()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        tokenTracker.Unregister(); // Dispose() cannot be used here because it's a blocking call
        tokenTracker = default;

        timeoutTracker.Unregister(); // Dispose() cannot be used here because it's a blocking call
        timeoutTracker = default;

        if (timeoutSource is not null && !timeoutSource.TryReset())
        {
            timeoutSource.Dispose();
            timeoutSource = null;
        }
    }

    private static void InvokeContinuation(object? capturedContext, Action<object?> continuation, object? state, bool runAsynchronously, bool flowExecutionContext)
    {
        switch (capturedContext)
        {
            case null:
                if (!runAsynchronously)
                    goto default;

                if (flowExecutionContext)
                    ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: true);
                else
                    ThreadPool.UnsafeQueueUserWorkItem(continuation, state, preferLocal: true);
                break;
            case SynchronizationContext context:
                context.Post(continuation.Invoke, state);
                break;
            case TaskScheduler scheduler:
                Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                break;
            default:
                continuation(state);
                break;
        }
    }

    private void InvokeContinuationCore(bool flowExecutionContext)
    {
        var continuation = this.continuation;
        this.continuation = null;

        var continuationState = this.continuationState;
        this.continuationState = null;

        var capturedContext = this.capturedContext;
        this.capturedContext = null;

        if (continuation is not null)
            InvokeContinuation(capturedContext, continuation, continuationState, runContinuationsAsynchronously, flowExecutionContext);
    }

    private static void InvokeContinuation(object? source)
    {
        Debug.Assert(source is ManualResetCompletionSource);

        Unsafe.As<ManualResetCompletionSource>(source).InvokeContinuationCore(flowExecutionContext: true);
    }

    private protected void InvokeContinuation()
    {
        var contextCopy = context;
        context = null;

        if (contextCopy is null)
            InvokeContinuationCore(flowExecutionContext: false);
        else
            ExecutionContext.Run(contextCopy, ContinuationInvoker, this);
    }

    private protected virtual void ResetCore()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        version += 1;
        completed = false;
        context = null;
        continuation = null;
        continuationState = capturedContext = null;
    }

    /// <summary>
    /// Attempts to reset state of this object for reuse.
    /// </summary>
    /// <remarks>
    /// This methods acts as a barried for completion.
    /// It means that calling of this method guarantees that the task
    /// cannot be completed by previously linked timeout or cancellation token.
    /// </remarks>
    /// <returns>The version of the incompleted task.</returns>
    public short Reset()
    {
        short result;

        lock (SyncRoot)
        {
            StopTrackingCancellation();
            ResetCore();
            result = version;
        }

        return result;
    }

    /// <summary>
    /// Invokes when this source is ready to reuse.
    /// </summary>
    protected virtual void AfterConsumed()
    {
    }

    /// <inheritdoc />
    void IThreadPoolWorkItem.Execute() => AfterConsumed();

    private protected void QueueAfterConsumed()
    {
        if (!ThreadPool.UnsafeQueueUserWorkItem(this, true))
            AfterConsumed();
    }

    private void OnCompleted(object? capturedContext, Action<object?> continuation, object? state, short token, bool flowExecutionContext)
    {
        // fast path - monitor lock is not needed
        if (token != version)
            goto invalid_token;

        if (completed)
            goto execute_inplace;

        lock (SyncRoot)
        {
            // avoid running continuation inside of the lock
            if (token != version)
                goto invalid_token;

            if (completed)
                goto execute_inplace;

            this.continuation = continuation;
            continuationState = state;
            this.capturedContext = capturedContext;
            context = flowExecutionContext ? ExecutionContext.Capture() : null;
            goto exit;
        }

    execute_inplace:
        InvokeContinuation(capturedContext, continuation, state, runContinuationsAsynchronously, flowExecutionContext);

    exit:
        return;
    invalid_token:
        throw new InvalidOperationException();
    }

    private protected void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        var capturedContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == 0 ? null : CaptureContext();
        OnCompleted(capturedContext, continuation, state, token, (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0);
    }

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public abstract bool TrySetException(Exception e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public abstract bool TrySetCanceled(CancellationToken token);

    /// <summary>
    /// Gets a value indicating that the source is in signaled state.
    /// </summary>
    public bool IsCompleted
    {
        get => completed;
        private protected set => completed = value;
    }

    private void PrepareTaskCore(TimeSpan timeout, CancellationToken token)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (timeout == TimeSpan.Zero)
        {
            CompleteAsTimedOut();
            goto exit;
        }

        if (token.IsCancellationRequested)
        {
            CompleteAsCanceled(token);
            goto exit;
        }

        StartTrackingCancellation(timeout, token);

    exit:
        return;
    }

    private protected void PrepareTask(TimeSpan timeout, CancellationToken token)
    {
        if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        if (!completed)
        {
            lock (SyncRoot)
            {
                if (!completed)
                    PrepareTaskCore(timeout, token);
            }
        }
    }
}