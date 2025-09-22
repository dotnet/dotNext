using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

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

        lock (SyncRoot)
        {
            if (versionAndStatus.Status is not ManualResetCompletionSourceStatus.Activated
                || versionAndStatus.Version != (short)expectedVersion
                || !(state.IsTimeoutToken(token) ? CompleteAsTimedOut() : CompleteAsCanceled(token)))
                goto exit;

            // ensure that timeout or cancellation handler sets the status correctly
            Debug.Assert(versionAndStatus.Status is ManualResetCompletionSourceStatus.WaitForConsumption);
        }

        NotifyConsumer();

        exit:
        return;
    }

    private protected abstract bool CompleteAsTimedOut();

    private protected abstract bool CompleteAsCanceled(CancellationToken token);

    /// <summary>
    /// Resets internal state of this source.
    /// </summary>
    protected virtual void CleanUp()
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
    /// This method acts as a barrier for completion.
    /// It means that calling of this method guarantees that the task
    /// cannot be completed by the previously linked timeout or cancellation token.
    /// </remarks>
    /// <returns>The version of the uncompleted task.</returns>
    public short Reset()
    {
        Monitor.Enter(SyncRoot);
        var stateCopy = ResetCore(out var token);
        Monitor.Exit(SyncRoot);

        stateCopy.Dispose();
        CleanUp();
        return token;
    }

    /// <summary>
    /// Attempts to reset the state of this source.
    /// </summary>
    /// <param name="token">The version of the uncompleted task.</param>
    /// <returns><see langword="true"/> if the state was reset successfully; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Reset"/>
    public bool TryReset(out short token)
    {
        bool result;

        if (result = Monitor.TryEnter(SyncRoot))
        {
            var stateCopy = ResetCore(out token);
            Monitor.Exit(SyncRoot);

            stateCopy.Dispose();
            CleanUp();
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
    protected internal void NotifyConsumer()
    {
        state.Detach().Dispose();

        if (continuation is { IsValid: true } c)
        {
            continuation = default;
            c.InvokeOnCapturedContext(runContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Moves this source to the completed state.
    /// </summary>
    /// <param name="completionData">Custom data to be record to <see cref="CompletionData"/> property.</param>
    /// <returns>
    /// <see langword="true"/> if the immediate caller must call <see cref="NotifyConsumer"/> to execute the callback;
    /// <see langword="false"/> to ignore invocation of <see cref="NotifyConsumer"/> method because the callback
    /// is not provided.
    /// </returns>
    private protected bool SetResult(object? completionData)
    {
        AssertLocked();

        CompletionData = completionData;
        versionAndStatus.Status = ManualResetCompletionSourceStatus.WaitForConsumption;
        return continuation.IsValid;
    }

    private void OnCompleted(in Continuation continuation, short token)
    {
        string errorMessage;

        // code block doesn't have any calls leading to exceptions
        // so replace try-finally with manually cloned code
        Monitor.Enter(SyncRoot);
        if (token != versionAndStatus.Version)
        {
            errorMessage = ExceptionMessages.InvalidSourceToken;
            Monitor.Exit(SyncRoot);
            goto invalid_state;
        }

        switch (versionAndStatus.Status)
        {
            default:
                errorMessage = ExceptionMessages.InvalidSourceState;
                Monitor.Exit(SyncRoot);
                goto invalid_state;
            case ManualResetCompletionSourceStatus.WaitForConsumption:
                Monitor.Exit(SyncRoot);
                break;
            case ManualResetCompletionSourceStatus.Activated:
                this.continuation = continuation;
                Monitor.Exit(SyncRoot);
                goto exit;
        }

        // execute continuation in-place because the source is completed already
        continuation.InvokeOnCurrentContext(runContinuationsAsynchronously);

    exit:
        return;
    invalid_state:
        throw new InvalidOperationException(errorMessage);
    }

    private protected ValueTaskSourceStatus GetStatus(short token, Exception? e)
    {
        var snapshot = versionAndStatus.VolatileRead(); // barrier to avoid reordering of result read

        if (token != snapshot.Version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        return !snapshot.IsCompleted
            ? ValueTaskSourceStatus.Pending
            : e switch
            {
                null => ValueTaskSourceStatus.Succeeded,
                OperationCanceledException => ValueTaskSourceStatus.Canceled,
                _ => ValueTaskSourceStatus.Faulted,
            };
    }

    /// <inheritdoc cref="IValueTaskSource.OnCompleted"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
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
    public bool TrySetCanceled(CancellationToken token)
        => TrySetException(new OperationCanceledException(token));

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="CompletionData"/> property that can be accessed from within <see cref="AfterConsumed"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled(object? completionData, CancellationToken token)
        => TrySetException(completionData, new OperationCanceledException(token));

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

    private protected short? Activate(TimeSpan timeout, CancellationToken token)
    {
        Timeout.Validate(timeout);

        short? result;
        lock (SyncRoot)
        {
            switch (versionAndStatus.Status)
            {
                case ManualResetCompletionSourceStatus.WaitForActivation:
                    if (timeout == TimeSpan.Zero)
                    {
                        CompleteAsTimedOut();
                    }
                    else if (!state.Initialize(ref versionAndStatus, cancellationCallback, timeout, token))
                    {
                        CompleteAsCanceled(token);
                    }

                    goto case ManualResetCompletionSourceStatus.WaitForConsumption;
                case ManualResetCompletionSourceStatus.WaitForConsumption:
                    result = versionAndStatus.Version;
                    break;
                default:
                    result = null;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Represents continuation attached by the task consumer.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Continuation : IThreadPoolWorkItem
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
                    schedulingContext = TaskScheduler.Current;
                    if (ReferenceEquals(schedulingContext, TaskScheduler.Default))
                        schedulingContext = null;
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
            else if (!runAsynchronously)
            {
                action(state);
            }
            else if (context is not null)
            {
                ThreadPool.QueueUserWorkItem(action, state, preferLocal: true);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(action, state, preferLocal: true);
            }
        }

        public void InvokeOnCapturedContext(bool runAsynchronously)
        {
            Debug.Assert(action is not null);

            if (schedulingContext is not null)
            {
                InvokeOnSchedulingContext();
            }
            else
            {
                switch (runAsynchronously, context is not null)
                {
                    case (true, true):
                        ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
                        break;
                    case (true, false):
                        ThreadPool.UnsafeQueueUserWorkItem(action, state, preferLocal: true);
                        break;
                    case (false, true):
                        Debug.Assert(context is not null);

                        // ContextCallback has the same signature as Action<object?> so we
                        // can reinterpret the reference
                        ExecutionContext.Run(context, Unsafe.As<ContextCallback>(action), state);
                        break;
                    default:
                        action(state);
                        break;
                }
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
            Debug.Assert(context is not null);

            // ThreadPool restores original execution context automatically
            // See https://github.com/dotnet/runtime/blob/cb30e97f8397e5f87adee13f5b4ba914cc2c0064/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPoolWorkQueue.cs#L928
            ExecutionContext.Restore(context);

            action(state);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private protected struct VersionAndStatus
    {
        private uint value;

        public VersionAndStatus()
            : this(InitialCompletionToken, ManualResetCompletionSourceStatus.WaitForActivation)
        {
        }

        private VersionAndStatus(short version, ManualResetCompletionSourceStatus status)
        {
            Debug.Assert(Enum.GetUnderlyingType(typeof(ManualResetCompletionSourceStatus)) == typeof(short));

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
        private static ref short GetVersion(ref uint value)
            => ref Unsafe.As<uint, short>(ref value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref ManualResetCompletionSourceStatus GetStatus(ref uint value)
            => ref Unsafe.As<short, ManualResetCompletionSourceStatus>(ref Unsafe.Add(ref Unsafe.As<uint, short>(ref value), 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Combine(short version, ManualResetCompletionSourceStatus status)
        {
            Unsafe.SkipInit(out uint result);
            GetVersion(ref result) = version;
            GetStatus(ref result) = status;

            return result;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct CancellationState : IDisposable
    {
        private CancellationTokenRegistration tokenTracker;
        private CancellationTokenSource? timeoutSource;

        internal bool Initialize(ref VersionAndStatus vs, Action<object?, CancellationToken> callback, TimeSpan timeout, CancellationToken token)
        {
            // box current token once and only if needed
            IBinaryInteger<short>? cachedVersion = null;

            if (token.CanBeCanceled)
            {
                tokenTracker = token.UnsafeRegister(callback, cachedVersion = vs.Version);
            }

            // This method may cause deadlock if token becomes canceled within the lock.
            // In this case, registration calls the callback synchronously.
            // The callback tries to acquire the lock but stuck.
            // To avoid that, change the status later and check the token after calling this method
            vs.Status = ManualResetCompletionSourceStatus.Activated;

            if (token.IsCancellationRequested)
                return false;
            
            if (timeout > default(TimeSpan))
            {
                timeoutSource ??= new();

                // TryReset() or Dispose() destroys active registration so it's not necessary
                // to keep CancellationTokenRegistration to save memory
                timeoutSource.Token.UnsafeRegister(callback, cachedVersion ?? vs.Version);
                timeoutSource.CancelAfter(timeout);
            }

            return true;
        }

        internal readonly bool IsTimeoutToken(CancellationToken token)
            => timeoutSource?.Token == token;

        internal CancellationState Detach()
        {
            var copy = new CancellationState
            {
                tokenTracker = tokenTracker,
            };

            // reuse CTS for timeout if possible
            if (timeoutSource is { } ts && !ts.TryReset())
            {
                copy.timeoutSource = ts;
                timeoutSource = null;
            }

            tokenTracker = default;
            return copy;
        }

        public readonly void Dispose()
        {
            // Unregister() doesn't block the caller in contrast to Dispose()
            tokenTracker.Unregister();
            timeoutSource?.Dispose();
        }
    }
}