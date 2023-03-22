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
public abstract partial class ManualResetCompletionSource
{
    /// <summary>
    /// Represents initial value of the completion token when constructing a new instance of the completion source.
    /// </summary>
    protected const short InitialCompletionToken = short.MinValue;

    private readonly Action<object?, CancellationToken> cancellationCallback;
    private readonly bool runContinuationsAsynchronously;
    private CancellationState state;

    // task management
    private Continuation continuation;
    private protected VersionAndStatus versionAndStatus;

    private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
    {
        this.runContinuationsAsynchronously = runContinuationsAsynchronously;
        versionAndStatus = new();

        // cached callback to avoid further allocations
        cancellationCallback = CancellationRequested;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CancellationRequested(object? expectedVersion, CancellationToken token)
    {
        Debug.Assert(expectedVersion is short);

        // due to concurrency, this method can be called after Reset or twice
        // that's why we need to skip the call if token doesn't match (call after Reset)
        // or completed flag is set (call twice with the same token)
        if (versionAndStatus.Status is not ManualResetCompletionSourceStatus.Activated)
            goto exit;

        EnterLock();
        try
        {
            if (versionAndStatus.Status is not ManualResetCompletionSourceStatus.Activated
                || versionAndStatus.Version != (short)expectedVersion
                || !(state.IsTimeoutToken(token) ? CompleteAsTimedOut() : CompleteAsCanceled(token)))
                goto exit;

            // ensure that timeout or cancellation handler sets the status correctly
            Debug.Assert(versionAndStatus.Status is ManualResetCompletionSourceStatus.WaitForConsumption);
        }
        finally
        {
            ExitLock();
        }

        Resume();

    exit:
        return;
    }

    private protected abstract bool CompleteAsTimedOut();

    private protected abstract bool CompleteAsCanceled(CancellationToken token);

    /// <summary>
    /// Resets internal state of this source.
    /// </summary>
    protected virtual void Cleanup()
    {
    }

    private CancellationState ResetCore(out short token)
    {
        AssertLocked();

        token = versionAndStatus.Reset();
        CompletionData = null;
        return state.Detach();
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
        EnterLock();
        var stateCopy = ResetCore(out var token);
        ExitLock();

        stateCopy.Cleanup();
        Cleanup();
        return token;
    }

    /// <summary>
    /// Attempts to reset the state of this source.
    /// </summary>
    /// <param name="token">The version of the incompleted task.</param>
    /// <returns><see langword="true"/> if the state was reset successfully; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Reset"/>
    public bool TryReset(out short token)
    {
        bool result;

        if (result = TryEnterLock())
        {
            var stateCopy = ResetCore(out token);
            ExitLock();

            stateCopy.Cleanup();
            Cleanup();
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
    protected object? CompletionData
    {
        get;
        private set;
    }

    /// <summary>
    /// Invokes continuation callback and cleanup state of this source.
    /// </summary>
    internal void Resume()
    {
        state.Detach().Cleanup();

        if (continuation is { IsValid: true } c)
        {
            continuation = default;
            c.Invoke(runContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Moves this source to the completed state.
    /// </summary>
    /// <param name="completionData">Custom data to be record to <see cref="CompletionData"/> property.</param>
    /// <returns>
    /// <see langword="true"/> if the immediate caller must call <see cref="Resume"/> to execute the callback;
    /// <see langword="false"/> to ignore invocation of <see cref="Resume"/> method because the callback
    /// is not provided.
    /// </returns>
    private protected bool SetResult(object? completionData)
    {
        CompletionData = completionData;
        versionAndStatus.Status = ManualResetCompletionSourceStatus.WaitForConsumption;
        return continuation.IsValid;
    }

    private void OnCompleted(in Continuation continuation, short token)
    {
        string errorMessage;

        // code block doesn't have any calls leading to exceptions
        // so replace try-finally with manually cloned code
        EnterLock();
        if (token != versionAndStatus.Version)
        {
            errorMessage = ExceptionMessages.InvalidSourceToken;
            ExitLock();
            goto invalid_state;
        }

        switch (versionAndStatus.Status)
        {
            default:
                errorMessage = ExceptionMessages.InvalidSourceState;
                ExitLock();
                goto invalid_state;
            case ManualResetCompletionSourceStatus.WaitForConsumption:
                ExitLock();
                break;
            case ManualResetCompletionSourceStatus.Activated:
                this.continuation = continuation;
                ExitLock();
                goto exit;
        }

        // execute continuation in-place because the source is completed already
        continuation.InvokeOnCurrentContext(runContinuationsAsynchronously);

    exit:
        return;
    invalid_state:
        throw new InvalidOperationException(errorMessage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    public ManualResetCompletionSourceStatus Status => versionAndStatus.VolatileRead().Status;

    /// <summary>
    /// Gets a value indicating that this source is in signaled (completed) state.
    /// </summary>
    /// <remarks>
    /// This property returns <see langword="true"/> if <see cref="Status"/> is <see cref="ManualResetCompletionSourceStatus.WaitForConsumption"/>
    /// or <see cref="ManualResetCompletionSourceStatus.Consumed"/>.
    /// </remarks>
    public bool IsCompleted => versionAndStatus.VolatileRead().IsCompleted;

    private protected short? PrepareTask(TimeSpan timeout, CancellationToken token)
    {
        if (timeout.Ticks is < 0L and not Timeout.InfiniteTicks)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        // The task can be created for the completed (but not yet consumed) source.
        // This workaround is needed for AsyncBridge methods
        short? result;
        EnterLock();
        try
        {
            switch (versionAndStatus.Status)
            {
                case ManualResetCompletionSourceStatus.WaitForActivation when timeout == default:
                    CompleteAsTimedOut();
                    goto case ManualResetCompletionSourceStatus.WaitForConsumption;
                case ManualResetCompletionSourceStatus.WaitForActivation:
                    state.Initialize(ref versionAndStatus, cancellationCallback, timeout, token);

                    if (token.IsCancellationRequested)
                        CompleteAsCanceled(token);

                    goto case ManualResetCompletionSourceStatus.WaitForConsumption;
                case ManualResetCompletionSourceStatus.WaitForConsumption:
                    result = versionAndStatus.Version;
                    break;
                default:
                    result = null;
                    break;
            }
        }
        finally
        {
            ExitLock();
        }

        return result;
    }

    /// <summary>
    /// Represents continuation attached by the task consumer.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private protected readonly struct Continuation : IThreadPoolWorkItem
    {
        private readonly Action<object?> action;
        private readonly object? state, schedulingContext;
        private readonly ExecutionContext? context;

        public Continuation(Action<object?> action, object? state, ValueTaskSourceOnCompletedFlags flags)
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

        public bool IsValid => action is not null;

        public void InvokeOnCurrentContext(bool runAsynchronously)
        {
            if (schedulingContext is not null)
            {
                Invoke();
            }
            else if (runAsynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
            }
            else
            {
                action(state);
            }
        }

        public void Invoke(bool runAsynchronously)
        {
            Debug.Assert(action is not null);

            if (schedulingContext is not null)
            {
                InvokeOnSchedulingContext();
            }
            else if (runAsynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
            }
            else if (context is { } ctx)
            {
                // ContextCallback has the same signature as Action<object?> so we
                // can reinterpret the reference
                ExecutionContext.Run(ctx, Unsafe.As<ContextCallback>(action), state);
            }
            else
            {
                action(state);
            }
        }

        private void InvokeOnSchedulingContext()
        {
            if (context is { } ctx)
            {
                var currentContext = ExecutionContext.Capture();
                ExecutionContext.Restore(ctx);

                try
                {
                    Invoke();
                }
                finally
                {
                    if (currentContext is not null)
                        ExecutionContext.Restore(currentContext);
                }
            }
            else
            {
                Invoke();
            }
        }

        private void Invoke()
        {
            Debug.Assert(schedulingContext is not null);

            switch (schedulingContext)
            {
                case SynchronizationContext context:
                    context.Post(action.Invoke, state);
                    break;
                case TaskScheduler scheduler:
                    Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
                default:
                    Debug.Fail($"Unexpected scheduling context {schedulingContext}");
                    break;
            }
        }

        void IThreadPoolWorkItem.Execute()
        {
            // ThreadPool restores original execution context automatically
            // See https://github.com/dotnet/runtime/blob/cb30e97f8397e5f87adee13f5b4ba914cc2c0064/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPoolWorkQueue.cs#L928
            if (context is { } ctx)
                ExecutionContext.Restore(ctx);

            action(state);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected struct VersionAndStatus
    {
        private ulong value;

        public VersionAndStatus()
            : this(InitialCompletionToken, ManualResetCompletionSourceStatus.WaitForActivation)
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
        public VersionAndStatus VolatileRead() => new() { value = Volatile.Read(ref value) };

        public bool IsCompleted => Status >= ManualResetCompletionSourceStatus.WaitForConsumption;

        public bool CanBeCompleted(short? token)
        {
            var actualToken = Version;

            return Status is ManualResetCompletionSourceStatus.WaitForActivation or ManualResetCompletionSourceStatus.Activated
                && token.GetValueOrDefault(actualToken) == actualToken;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Consume(short version)
        {
            // LOCK CMPXCHG (x86) or CASAL (ARM) provides full memory fence. This what we actually
            // need because Consume method orders LOAD copy of the task result and OnConsumed that
            // triggers Reset() and erasure of the task result in the right way
            var actual = Interlocked.CompareExchange(ref value, Combine(version, ManualResetCompletionSourceStatus.Consumed), Combine(version, ManualResetCompletionSourceStatus.WaitForConsumption));

            string errorMessage;
            if (GetStatus(ref actual) is not ManualResetCompletionSourceStatus.WaitForConsumption)
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

    [StructLayout(LayoutKind.Auto)]
    private struct CancellationState
    {
        private CancellationTokenRegistration tokenTracker, timeoutTracker;
        private CancellationTokenSource? timeoutSource;

        internal void Initialize(ref VersionAndStatus vs, Action<object?, CancellationToken> callback, TimeSpan timeout, CancellationToken token)
        {
            // box current token once and only if needed
            var cachedVersion = default(IEquatable<short>);

            if (token.CanBeCanceled)
            {
                tokenTracker = token.UnsafeRegister(callback, cachedVersion = vs.Version);
                Interlocked.MemoryBarrier();
            }

            // This method may cause deadlock if token becomes canceled within the lock.
            // In this case, registration calls the callback synchronously.
            // The callback tries to acquire the lock but stuck.
            // To avoid that, change the status later using write barrier and check the token later
            vs.Status = ManualResetCompletionSourceStatus.Activated;

            if (timeout > default(TimeSpan) && !token.IsCancellationRequested)
            {
                timeoutSource ??= new();
                timeoutTracker = timeoutSource.Token.UnsafeRegister(callback, cachedVersion ?? vs.Version);
                timeoutSource.CancelAfter(timeout);
            }
        }

        internal readonly bool IsTimeoutToken(CancellationToken token)
            => timeoutSource?.Token == token;

        internal CancellationState Detach()
        {
            var copy = new CancellationState
            {
                tokenTracker = tokenTracker,
                timeoutTracker = timeoutTracker,
                timeoutSource = timeoutSource is { } ts && !ts.TryReset() ? ts : null,
            };

            this = default;
            return copy;
        }

        internal readonly void Cleanup()
        {
            // Unregister() doesn't block the caller in contrast to Dispose()
            tokenTracker.Unregister();
            timeoutTracker.Unregister();
            timeoutSource?.Dispose();
        }
    }
}