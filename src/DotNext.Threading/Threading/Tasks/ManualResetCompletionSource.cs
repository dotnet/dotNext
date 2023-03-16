using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ValueTaskSourceOnCompletedFlags = System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents base class for producer of value task.
/// </summary>
[SuppressMessage("Usage", "CA1001", Justification = "CTS is disposed automatically when passing through lifecycle of the completion source")]
public abstract class ManualResetCompletionSource
{
    private readonly Action<object?, CancellationToken> cancellationCallback;
    private protected readonly bool runContinuationsAsynchronously;
    private CancellationTokenRegistration tokenTracker, timeoutTracker;
    private CancellationTokenSource? timeoutSource;

    // task management
    private Continuation continuation;
    private object? completionData;
    private protected VersionAndStatus versionAndStatus;

    private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
    {
        this.runContinuationsAsynchronously = runContinuationsAsynchronously;
        versionAndStatus = new();

        // cached callback to avoid further allocations
        cancellationCallback = CancellationRequested;
    }

    private protected object SyncRoot => cancellationCallback;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CancellationRequested(object? expectedVersion, CancellationToken token)
    {
        Debug.Assert(expectedVersion is short);

        // due to concurrency, this method can be called after Reset or twice
        // that's why we need to skip the call if token doesn't match (call after Reset)
        // or completed flag is set (call twice with the same token)
        if (versionAndStatus.Status is ManualResetCompletionSourceStatus.Activated)
        {
            Continuation continuation;
            lock (SyncRoot)
            {
                continuation = versionAndStatus.Check((short)expectedVersion, ManualResetCompletionSourceStatus.Activated)
                    ? timeoutSource?.Token == token
                    ? CompleteAsTimedOut()
                    : CompleteAsCanceled(token)
                    : default;
            }

            if (continuation)
                continuation.Invoke(runContinuationsAsynchronously);
        }
    }

    private protected void StartTrackingCancellation(TimeSpan timeout, CancellationToken token)
    {
        // box current token once and only if needed
        var tokenHolder = default(IEquatable<short>);
        if (timeout > default(TimeSpan))
        {
            timeoutSource ??= new();
            timeoutTracker = timeoutSource.Token.UnsafeRegister(cancellationCallback, tokenHolder = versionAndStatus.Version);
            timeoutSource.CancelAfter(timeout);
        }

        if (token.CanBeCanceled)
        {
            tokenTracker = token.UnsafeRegister(cancellationCallback, tokenHolder ?? versionAndStatus.Version);
        }
    }

    private protected abstract Continuation CompleteAsTimedOut();

    private protected abstract Continuation CompleteAsCanceled(CancellationToken token);

    private void StopTrackingCancellation()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        // Dispose() cannot be used here because it's a blocking call that could lead to deadlock
        // because of concurrency with CancellationRequested
        tokenTracker.Unregister();
        tokenTracker = default;

        timeoutTracker.Unregister();
        timeoutTracker = default;

        if (timeoutSource is not null && !timeoutSource.TryReset())
        {
            timeoutSource.Dispose();
            timeoutSource = null;
        }
    }

    /// <summary>
    /// Resets internal state of this source.
    /// </summary>
    protected virtual void Cleanup()
    {
    }

    private short ResetCore()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        StopTrackingCancellation();
        completionData = null;
        return versionAndStatus.Reset();
    }

    /// <summary>
    /// Resets the state of the source.
    /// </summary>
    /// <remarks>
    /// This methods acts as a barrier for completion.
    /// It means that calling of this method guarantees that the task
    /// cannot be completed by the previously linked timeout or cancellation token.
    /// </remarks>
    /// <returns>The version of the incompleted task.</returns>
    public short Reset()
    {
        short result;

        lock (SyncRoot)
        {
            result = ResetCore();
            Cleanup();
        }

        return result;
    }

    /// <summary>
    /// Attempts to reset the state of this source.
    /// </summary>
    /// <param name="token">The version of the incompleted task.</param>
    /// <returns><see langword="true"/> if the state was reset successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryReset(out short token)
    {
        bool result;
        if (result = Monitor.TryEnter(SyncRoot))
        {
            try
            {
                token = ResetCore();
                Cleanup();
            }
            finally
            {
                Monitor.Exit(SyncRoot);
            }
        }
        else
        {
            token = default;
        }

        return result;
    }

    /// <summary>
    /// Invokes when this source is ready to reuse.
    /// </summary>
    /// <seealso cref="CompletionData"/>
    protected virtual void AfterConsumed()
    {
    }

    /// <summary>
    /// Gets a value passed to the manual completion method.
    /// </summary>
    protected object? CompletionData => completionData;

    private protected Continuation Complete(object? completionData)
    {
        StopTrackingCancellation();
        this.completionData = completionData;
        versionAndStatus.Status = ManualResetCompletionSourceStatus.WaitForConsumption;

        var result = continuation;
        continuation = default;
        return result;
    }

    private void OnCompleted(in Continuation continuation, short token)
    {
        string errorMessage;
        var snapshot = versionAndStatus;

        // fast path - monitor lock is not needed
        if (token != snapshot.Version)
        {
            errorMessage = ExceptionMessages.InvalidSourceToken;
            goto invalid_state;
        }

        switch (snapshot.Status)
        {
            default:
                errorMessage = ExceptionMessages.InvalidSourceState;
                goto invalid_state;
            case ManualResetCompletionSourceStatus.WaitForConsumption:
                goto execute_inplace;
            case ManualResetCompletionSourceStatus.Activated:
                break;
        }

        lock (SyncRoot)
        {
            // avoid running continuation inside of the lock
            if (token != versionAndStatus.Version)
            {
                errorMessage = ExceptionMessages.InvalidSourceToken;
                goto invalid_state;
            }

            switch (versionAndStatus.Status)
            {
                default:
                    errorMessage = ExceptionMessages.InvalidSourceState;
                    goto invalid_state;
                case ManualResetCompletionSourceStatus.WaitForConsumption:
                    goto execute_inplace;
                case ManualResetCompletionSourceStatus.Activated:
                    break;
            }

            this.continuation = continuation;
            goto exit;
        }

    execute_inplace:
        continuation.InvokeWithinCurrentContext(runContinuationsAsynchronously);

    exit:
        return;
    invalid_state:
        throw new InvalidOperationException(errorMessage);
    }

    private protected void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => OnCompleted(new(continuation, state, flags), token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetException(Exception e) => TrySetException(null, e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="CompletionData"/> property that can be accessed from within <see cref="AfterConsumed"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public abstract bool TrySetException(object? completionData, Exception e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetCanceled(CancellationToken token) => TrySetCanceled(null, token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="CompletionData"/> property that can be accessed from within <see cref="AfterConsumed"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public abstract bool TrySetCanceled(object? completionData, CancellationToken token);

    /// <summary>
    /// Gets the status of this source.
    /// </summary>
    public ManualResetCompletionSourceStatus Status => versionAndStatus.ReadStatusVolatile();

    /// <summary>
    /// Gets a value indicating that this source is in signaled (completed) state.
    /// </summary>
    /// <remarks>
    /// This property returns <see langword="true"/> if <see cref="Status"/> is <see cref="ManualResetCompletionSourceStatus.WaitForConsumption"/>
    /// or <see cref="ManualResetCompletionSourceStatus.Consumed"/>.
    /// </remarks>
    public bool IsCompleted => versionAndStatus.IsCompleted;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareTaskCore(TimeSpan timeout, CancellationToken token)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        if (timeout == default)
        {
            CompleteAsTimedOut();
        }
        else if (token.IsCancellationRequested)
        {
            CompleteAsCanceled(token);
        }
        else
        {
            versionAndStatus.Status = ManualResetCompletionSourceStatus.Activated;
            StartTrackingCancellation(timeout, token);
        }
    }

    private protected bool PrepareTask(TimeSpan timeout, CancellationToken token)
    {
        if (timeout.Ticks is < 0L and not Timeout.InfiniteTicks)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        bool result;

        // The task can be created for the completed source. This workaround is needed for AsyncBridge methods
        lock (SyncRoot)
        {
            switch (versionAndStatus.Status)
            {
                case ManualResetCompletionSourceStatus.WaitForActivation:
                    PrepareTaskCore(timeout, token);
                    goto case ManualResetCompletionSourceStatus.WaitForConsumption;
                case ManualResetCompletionSourceStatus.WaitForConsumption:
                    result = true;
                    break;
                default:
                    result = false;
                    break;
            }
        }

        return result;
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private protected static void InvalidSourceStateDetected()
        => throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    // encapsulates continuation and its execution logic
    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct Continuation
    {
        private readonly Action<object?> action;
        private readonly object? state, schedulingContext;
        private readonly ExecutionContext? context;

        internal Continuation(Action<object?> action, object? state, ValueTaskSourceOnCompletedFlags flags)
        {
            Debug.Assert(action is not null);

            this.action = action;
            this.state = state;

            schedulingContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) is not 0
                ? CaptureSchedulingContext()
                : null;

            context = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) is not 0
                ? ExecutionContext.Capture()
                : null;

            static object? CaptureSchedulingContext()
            {
                object? schedulingContext = SynchronizationContext.Current;
                if (schedulingContext is null || schedulingContext.GetType() == typeof(SynchronizationContext))
                {
                    var scheduler = TaskScheduler.Current;
                    schedulingContext = ReferenceEquals(scheduler, TaskScheduler.Default) ? null : scheduler;
                }

                return schedulingContext;
            }
        }

        private void Invoke(bool runAsynchronously, bool flowExecutionContext)
        {
            switch (schedulingContext)
            {
                case null when runAsynchronously:
                    if (flowExecutionContext)
                        ThreadPool.QueueUserWorkItem(action, state, preferLocal: true);
                    else
                        ThreadPool.UnsafeQueueUserWorkItem(action, state, preferLocal: true);
                    break;
                case SynchronizationContext context:
                    context.Post(action.Invoke, state);
                    break;
                case TaskScheduler scheduler:
                    Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
                default:
                    action(state);
                    break;
            }
        }

        internal void InvokeWithinCurrentContext(bool runAsynchronously) => Invoke(runAsynchronously, context is not null);

        internal void Invoke(bool runAsynchronously)
        {
            if (context is null)
            {
                Invoke(runAsynchronously, flowExecutionContext: false);
            }
            else
            {
                // flow should not be suppressed
                var currentContext = ExecutionContext.Capture() ?? throw new InvalidOperationException(ExceptionMessages.FlowSuppressed);
                ExecutionContext.Restore(context);

                try
                {
                    Invoke(runAsynchronously, flowExecutionContext: true);
                }
                finally
                {
                    ExecutionContext.Restore(currentContext);
                }
            }
        }

        public static bool operator true(in Continuation continuation) => continuation.action is not null;

        public static bool operator false(in Continuation continuation) => continuation.action is null;
    }

    [StructLayout(LayoutKind.Auto)]
    private protected struct VersionAndStatus
    {
        private ulong value;

        public VersionAndStatus()
            : this(short.MinValue, ManualResetCompletionSourceStatus.WaitForActivation)
        {
        }

        private VersionAndStatus(short version, ManualResetCompletionSourceStatus status)
        {
            Debug.Assert(Enum.GetUnderlyingType(typeof(ManualResetCompletionSourceStatus)) == typeof(int));

            value = Combine(version, status);
        }

        public short Version => GetVersion(ref value);

        public ManualResetCompletionSourceStatus Status
        {
            get => GetStatus(ref value);
            set => GetStatus(ref this.value) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ManualResetCompletionSourceStatus ReadStatusVolatile()
        {
            var copy = Volatile.Read(ref value);
            return GetStatus(ref copy);
        }

        public bool IsCompleted => Status >= ManualResetCompletionSourceStatus.WaitForConsumption;

        public bool CanBeCompleted => Status is ManualResetCompletionSourceStatus.WaitForActivation or ManualResetCompletionSourceStatus.Activated;

        public bool Check(short version, ManualResetCompletionSourceStatus status)
            => value == Combine(version, status);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Consume(short version)
        {
            // LOCK CMPXCHG (x86) or CASAL (ARM) provides full memory fence. This what we actually
            // need because Consume method orders LOAD copy of the task result and OnConsumed that
            // triggers Reset() and erasure of the task result in the right way
            var actual = Interlocked.CompareExchange(ref value, Combine(version, ManualResetCompletionSourceStatus.Consumed), Combine(version, ManualResetCompletionSourceStatus.WaitForConsumption));

            string errorMessage;
            if (GetStatus(ref actual) != ManualResetCompletionSourceStatus.WaitForConsumption)
            {
                errorMessage = ExceptionMessages.InvalidSourceState;
            }
            else if (GetVersion(ref actual) != version)
            {
                errorMessage = ExceptionMessages.InvalidSourceToken;
            }
            else
            {
                return;
            }

            throw new InvalidOperationException(errorMessage);
        }

        public short Reset()
        {
            var version = GetVersion(ref value);

            // write atomically
            value = Combine(++version, ManualResetCompletionSourceStatus.WaitForActivation);
            return version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref short GetVersion(ref ulong value)
            => ref Unsafe.As<ulong, short>(ref value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref ManualResetCompletionSourceStatus GetStatus(ref ulong value)
            => ref Unsafe.As<int, ManualResetCompletionSourceStatus>(ref Unsafe.Add(ref Unsafe.As<ulong, int>(ref value), 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Combine(short version, ManualResetCompletionSourceStatus status)
        {
            Unsafe.SkipInit(out ulong result);
            GetVersion(ref result) = version;
            GetStatus(ref result) = status;

            return result;
        }
    }
}