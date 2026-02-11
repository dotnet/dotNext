using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents base class for producer of value task.
/// </summary>
public abstract partial class ManualResetCompletionSource
{
    /// <summary>
    /// Represents initial value of the completion token when constructing a new instance of the completion source.
    /// </summary>
    protected const short InitialCompletionToken = short.MinValue;

    private readonly Action<object?, CancellationToken> cancellationCallback;
    private readonly bool runContinuationsAsynchronously;
    private CancellationState state; // protected by activation states
    private Continuation continuation; // protected by subscription states

    private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
    {
        this.runContinuationsAsynchronously = runContinuationsAsynchronously;
        syncState = unchecked((uint)(ushort)InitialCompletionToken << 16);

        // cached callback to avoid further allocations
        cancellationCallback = CancellationRequested;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CancellationRequested(object? expectedVersion, CancellationToken token)
    {
        Debug.Assert(expectedVersion is short);

        if (BeginCompletion((short)expectedVersion))
        {
            try
            {
                if (state.IsTimeoutToken(token))
                {
                    CompleteAsTimedOut();
                }
                else
                {
                    CompleteAsCanceled(token);
                }
            }
            finally
            {
                if (EndCompletion())
                {
                    NotifyConsumer();
                }
            }
        }
    }

    private protected abstract void CompleteAsTimedOut();

    private protected abstract void CompleteAsCanceled(CancellationToken token);

    /// <summary>
    /// Resets internal state of this source.
    /// </summary>
    protected virtual void CleanUp()
    {
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
        var newVersion = ResetCore();
        CompletionData = null;
        state.Detach().Dispose();
        CleanUp();
        return newVersion;
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
    public object? CompletionData
    {
        get;
        private set; // protected by completion states
    }

    internal void NotifyConsumer()
    {
        state.Detach().Dispose();

        var continuationCopy = continuation;
        continuation = default;
        continuationCopy.InvokeOnCapturedContext(runContinuationsAsynchronously);
    }

    private void OnCompleted(in Continuation continuation, short expectedToken)
    {
        if (BeginSubscription(expectedToken))
        {
            this.continuation = continuation;

            if (!EndSubscription())
                return;
        }

        // execute continuation in-place because the source is completed already
        Debug.Assert(IsCompleted);
        continuation.InvokeOnCurrentContext(runContinuationsAsynchronously);
    }

    private protected ValueTaskSourceStatus GetStatus<TExceptionProvider>(short expectedToken, TExceptionProvider provider)
        where TExceptionProvider : ISupplier<Exception?>, allows ref struct
    {
        var stateCopy = Volatile.Read(in syncState);

        if (GetVersion(stateCopy) != expectedToken)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        // CompletedState acts as a barrier for written exception. Thus, we should not access the exception
        // before this check.
        if ((stateCopy & CompletedState) is not CompletedState)
            return ValueTaskSourceStatus.Pending;

        return provider.Invoke() switch
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
    public bool TrySetException(Exception e) => TrySetException(new DefaultOptions(), e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <param name="options">The completion options.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public abstract bool TrySetException<TOptions>(TOptions options, Exception e)
        where TOptions : ICompletionOptions, allows ref struct;

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
    /// <param name="options">The completion options.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled<TOptions>(TOptions options, CancellationToken token)
        where TOptions : ICompletionOptions, allows ref struct
        => TrySetException(options, new OperationCanceledException(token));

    /// <summary>
    /// Gets the status of this source.
    /// </summary>
    public ManualResetCompletionSourceStatus Status
    {
        get
        {
            var stateCopy = Volatile.Read(in syncState) & ~VersionMask;

            if (stateCopy is 0 or ActivatingState or (ActivatingState | CompletingState))
                return ManualResetCompletionSourceStatus.WaitForActivation;

            if ((stateCopy & ConsumedState) is not 0)
                return ManualResetCompletionSourceStatus.Consumed;

            return (stateCopy & CompletedState) is CompletedState
                ? ManualResetCompletionSourceStatus.WaitForConsumption
                : ManualResetCompletionSourceStatus.Activated;
        }
    }

    /// <summary>
    /// Gets a value indicating that this source is in signaled (completed) state.
    /// </summary>
    /// <remarks>
    /// This property returns <see langword="true"/> if <see cref="Status"/> is <see cref="ManualResetCompletionSourceStatus.WaitForConsumption"/>
    /// or <see cref="ManualResetCompletionSourceStatus.Consumed"/>.
    /// </remarks>
    public bool IsCompleted => (Volatile.Read(in syncState) & CompletedState) is CompletedState;

    private protected short Activate(TimeSpan timeout, CancellationToken token)
    {
        Timeout.Validate(timeout);

        if (BeginActivation(out var version))
        {
            try
            {
                state.Initialize(version, cancellationCallback, timeout, token);
            }
            finally
            {
                EndActivation();
            }
        }

        return version;
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
                ? ContinuationHelpers.CaptureSchedulingContext()
                : null;

            context = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) is not 0
                ? ExecutionContext.Capture()
                : null;
        }

        public void InvokeOnCurrentContext(bool runAsynchronously)
        {
            if (schedulingContext is not null)
            {
                action.InvokeInCurrentExecutionContext(state, schedulingContext);
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
                action.InvokeInExecutionContext(state, schedulingContext, context);
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

        void IThreadPoolWorkItem.Execute()
        {
            Debug.Assert(context is not null);

            // ThreadPool restores the original execution context automatically
            // See https://github.com/dotnet/runtime/blob/cb30e97f8397e5f87adee13f5b4ba914cc2c0064/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPoolWorkQueue.cs#L928
            ExecutionContext.Restore(context);

            action(state);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct CancellationState : IDisposable
    {
        private CancellationTokenRegistration tokenTracker;
        private CancellationTokenSource? timeoutSource;

        internal void Initialize(short version, Action<object?, CancellationToken> callback, TimeSpan timeout, CancellationToken token)
        {
            // box current token once and only if needed
            IBinaryInteger<short>? cachedVersion = null;

            if (token.CanBeCanceled)
            {
                tokenTracker = token.UnsafeRegister(callback, cachedVersion = version);
            }

            if (!token.IsCancellationRequested && timeout > TimeSpan.Zero)
            {
                timeoutSource ??= new();

                // TryReset() or Dispose() destroys active registration so it's not necessary
                // to keep CancellationTokenRegistration to save memory
                timeoutSource.Token.UnsafeRegister(callback, cachedVersion ?? version);
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