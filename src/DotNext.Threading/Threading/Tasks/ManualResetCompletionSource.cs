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
    
    private readonly bool runContinuationsAsynchronously;
    
    // protected by activation states
    private CancellationTokenRegistration tokenTracker;
    private IBinaryInteger<short>? cachedVersion;
    
    // protected by subscription states
    private Continuation continuation;

    private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
    {
        this.runContinuationsAsynchronously = runContinuationsAsynchronously;
        syncState = unchecked((uint)(ushort)InitialCompletionToken << 16);
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
    /// <para>
    /// The source can be reset when the previously created task is already consumed by its consumer,
    /// or when no consumer is attached to the task (a pending task can be abandoned this way).
    /// Calling this method while a consumer is awaiting the current task and the task is not yet
    /// consumed is not allowed: the awaiting consumer would never be resumed. In this case,
    /// the method throws <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    /// <returns>The version of the uncompleted task.</returns>
    /// <exception cref="InvalidOperationException">A consumer is subscribed to the current task, and the task is not yet consumed.</exception>
    public short Reset()
    {
        var newVersion = ResetCore();
        CompletionData = null;
        ResetCancellationState();
        continuation = default;
        CleanUp();
        return newVersion;
    }

    /// <summary>
    /// Invokes when this source is ready to reuse.
    /// </summary>
    /// <remarks>
    /// The default implementation releases the linked cancellation token registration,
    /// the timeout timer, and the captured continuation, so the consumed source is not
    /// rooted by them. In contrast to <see cref="Reset()"/>, it doesn't change the observable
    /// state of the source: the version, <see cref="Status"/>, and <see cref="CompletionData"/>
    /// remain intact until <see cref="Reset()"/> is called explicitly.
    /// </remarks>
    /// <seealso cref="CompletionData"/>
    protected virtual void AfterConsumed()
    {
        // release external roots: the CTS registration, the timer, and the captured continuation.
        // Consumption is causally after the notification, so these fields are exclusive here.
        ResetCancellationState();
        continuation = default;
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
        var continuationCopy = continuation;
        continuation = default;
        continuationCopy.InvokeOnCapturedContext(runContinuationsAsynchronously);
    }

    private void ResetCancellationState()
    {
        if (timeoutTracker is { } timer && !TryReset(timer, completedByTimeout))
        {
            timer.Dispose();
            timeoutTracker = null;
            cachedVersion = null;
        }

        completedByTimeout = false;

        if (!tokenTracker.UnregisterAndReuse())
        {
            cachedVersion = null;
            timeoutTracker?.Dispose();
            timeoutTracker = null;
        }
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
        => (timeout.Ticks, token.CanBeCanceled) switch
        {
            (Timeout.InfiniteTicks, false) => Activate(new NoOpActivation()),
            (Timeout.InfiniteTicks, true) => Activate(new CancellationTokenActivation(token)),
            (0L, _) => Activate(new TimedOutActivation()),
            (> 0L and < Timeout.MaxTimeoutParameterTicks, false)
                => Activate(new TimeoutActivation(timeout)),
            (> 0L and < Timeout.MaxTimeoutParameterTicks, true)
                => Activate(new TimeoutAndCancellationTokenActivation(timeout, token)),
            _ => throw new ArgumentOutOfRangeException(nameof(timeout))
        };

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
}

file static class CancellationTokenRegistrationExtensions
{
    public static bool UnregisterAndReuse(this ref CancellationTokenRegistration registration)
    {
        var token = registration.Token;

        // Unregister() doesn't block the caller in contrast to Dispose()
        bool unregistered;
        if (!token.CanBeCanceled)
        {
            unregistered = true;
        }
        else if (LinkedCancellationTokenSource.CanInlineToken)
        {
            var source = Unsafe.BitCast<CancellationToken, ValueTuple<CancellationTokenSource>>(token).Item1;
            
            // If unregistered, the callback cannot be invoked by cancellation.
            // Otherwise, the callback can be in-flight.
            unregistered = registration.Unregister() || !source.IsCancellationRequested || IsCancellationCompleted(source);
        }
        else
        {
            registration.Unregister();
            unregistered = false;
        }

        registration = default;
        return unregistered;

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = $"get_IsCancellationCompleted")]
        static extern bool IsCancellationCompleted(CancellationTokenSource source);
    }
}