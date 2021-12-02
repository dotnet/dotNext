using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

using NullExceptionConstant = Generic.DefaultConst<Exception?>;

/// <summary>
/// Represents the producer side of <see cref="ValueTask"/>.
/// </summary>
/// <remarks>
/// See description of <see cref="ValueTaskCompletionSource{T}"/> for more information
/// about behavior of the completion source.
/// </remarks>
/// <seealso cref="ValueTaskCompletionSource{T}"/>
public class ValueTaskCompletionSource : ManualResetCompletionSource, IValueTaskSource, ISupplier<TimeSpan, CancellationToken, ValueTask>
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct OperationCanceledExceptionFactory : ISupplier<OperationCanceledException>
    {
        private readonly CancellationToken token;

        internal OperationCanceledExceptionFactory(CancellationToken token) => this.token = token;

        OperationCanceledException ISupplier<OperationCanceledException>.Invoke()
            => new(token);

        public static implicit operator OperationCanceledExceptionFactory(CancellationToken token)
            => new(token);
    }

    private sealed class LinkedTaskCompletionSource : TaskCompletionSource
    {
        private static readonly Action<object?> CompletionCallback = OnCompleted;

        private static void OnCompleted(object? state)
        {
            Debug.Assert(state is LinkedTaskCompletionSource);

            Unsafe.As<LinkedTaskCompletionSource>(state).OnCompleted();
        }

        private IValueTaskSource? source;
        private short version;

        internal LinkedTaskCompletionSource(object? state)
            : base(state, TaskCreationOptions.None)
        {
        }

        internal void LinkTo(IValueTaskSource source, short version)
        {
            this.source = source;
            this.version = version;
            source.OnCompleted(CompletionCallback, this, version, ValueTaskSourceOnCompletedFlags.None);
        }

        private void OnCompleted()
        {
            if (source is not null)
            {
                try
                {
                    source.GetResult(version);
                    TrySetResult();
                }
                catch (OperationCanceledException e)
                {
                    TrySetCanceled(e.CancellationToken);
                }
                catch (Exception e)
                {
                    TrySetException(e);
                }
            }

            source = null;
        }
    }

    private static readonly NullExceptionConstant NullSupplier = new();

    // null - success, not null - error
    private ExceptionDispatchInfo? result;

    /// <summary>
    /// Initializes a new completion source.
    /// </summary>
    /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
    public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    private void SetResult(Exception? result)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        StopTrackingCancellation();
        this.result = result is null ? null : ExceptionDispatchInfo.Capture(result);
        OnCompleted();
        InvokeContinuation();
    }

    private protected sealed override void CompleteAsTimedOut()
        => SetResult(OnTimeout());

    private protected sealed override void CompleteAsCanceled(CancellationToken token)
        => SetResult(OnCanceled(token));

    private protected override void ResetCore()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        base.ResetCore();
        result = null;
    }

    /// <summary>
    /// Called automatically when timeout detected.
    /// </summary>
    /// <remarks>
    /// By default, this method returns <see cref="TimeoutException"/> as the task result.
    /// </remarks>
    /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
    protected virtual Exception? OnTimeout() => new TimeoutException();

    /// <summary>
    /// Called automatically when cancellation detected.
    /// </summary>
    /// <remarks>
    /// By default, this method returns <see cref="OperationCanceledException"/> as the task result.
    /// </remarks>
    /// <param name="token">The token representing cancellation reason.</param>
    /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
    protected virtual Exception? OnCanceled(CancellationToken token) => new OperationCanceledException(token);

    private bool TrySetResult<TFactory>(TFactory factory, short? completionToken = null)
        where TFactory : notnull, ISupplier<Exception?>
    {
        bool result;
        if (result = CanBeCompleted)
        {
            lock (SyncRoot)
            {
                if (result = CanBeCompleted && completionToken.GetValueOrDefault(version) == version)
                    SetResult(factory.Invoke());
            }
        }

        return result;
    }

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public sealed override bool TrySetCanceled(CancellationToken token)
        => TrySetResult<OperationCanceledExceptionFactory>(token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled(short completionToken, CancellationToken token)
        => TrySetResult<OperationCanceledExceptionFactory>(token, completionToken);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public sealed override bool TrySetException(Exception e)
        => TrySetResult<ValueSupplier<Exception>>(e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetException(short completionToken, Exception e)
        => TrySetResult<ValueSupplier<Exception>>(e, completionToken);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult()
        => TrySetResult(NullSupplier);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(short completionToken)
        => TrySetResult(NullSupplier, completionToken);

    /// <summary>
    /// Creates a fresh task linked with this source.
    /// </summary>
    /// <remarks>
    /// This method must be called after <see cref="ManualResetCompletionSource.Reset()"/>.
    /// </remarks>
    /// <param name="timeout">The timeout associated with the task.</param>
    /// <param name="token">The cancellation token that can be used to cancel the task.</param>
    /// <returns>A fresh incompleted task.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than zero but not equals to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</exception>
    public ValueTask CreateTask(TimeSpan timeout, CancellationToken token)
    {
        PrepareTask(timeout, token);
        return new(this, version);
    }

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token)
    {
        if (Status != ManualResetCompletionSourceStatus.WaitForConsumption)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

        if (token != version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        Thread.MemoryBarrier();

        OnConsumed<ValueTaskCompletionSource>();
        resultCopy?.Throw();
    }

    /// <inheritdoc />
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
    {
        if (token != version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        return !IsCompleted ? ValueTaskSourceStatus.Pending : result switch
        {
            null => ValueTaskSourceStatus.Succeeded,
            { SourceException: OperationCanceledException } => ValueTaskSourceStatus.Canceled,
            _ => ValueTaskSourceStatus.Faulted,
        };
    }

    /// <inheritdoc />
    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => OnCompleted(continuation, state, token, flags);

    /// <summary>
    /// Creates a linked <see cref="TaskCompletionSource"/> that can be used cooperatively to
    /// complete the task.
    /// </summary>
    /// <param name="userData">The custom data to be associated with the current version of the task.</param>
    /// <param name="timeout">The timeout associated with the task.</param>
    /// <param name="token">The cancellation token that can be used to cancel the task.</param>
    /// <returns>A linked <see cref="TaskCompletionSource"/>.</returns>
    public TaskCompletionSource CreateLinkedTaskCompletionSource(object? userData, TimeSpan timeout, CancellationToken token)
    {
        var source = new LinkedTaskCompletionSource(userData);
        PrepareTask(timeout, token);
        source.LinkTo(this, version);
        return source;
    }
}