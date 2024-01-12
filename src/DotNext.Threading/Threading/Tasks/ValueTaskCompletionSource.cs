using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

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

    private bool SetResult(Exception? result, object? completionData = null)
    {
        AssertLocked();

        this.result = result is null ? null : ExceptionDispatchInfo.Capture(result);
        return SetResult(completionData);
    }

    private protected sealed override bool CompleteAsTimedOut()
        => SetResult(OnTimeout());

    private protected sealed override bool CompleteAsCanceled(CancellationToken token)
        => SetResult(OnCanceled(token));

    /// <inheritdoc />
    protected override void Cleanup() => result = null;

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

    private bool SetResult<TFactory>(object? completionData, short? completionToken, TFactory factory)
        where TFactory : notnull, ISupplier<Exception?>
    {
        bool result;
        EnterLock();
        try
        {
            result = versionAndStatus.CanBeCompleted(completionToken);

            if (!result || !SetResult(factory.Invoke(), completionData))
                goto exit;
        }
        finally
        {
            ExitLock();
        }

        Resume();

    exit:
        return result;
    }

    /// <inheritdoc />
    public sealed override bool TrySetCanceled(object? completionData, CancellationToken token)
        => SetResult<OperationCanceledExceptionFactory>(completionData, completionToken: null, token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetCanceled(short completionToken, CancellationToken token)
        => TrySetCanceled(null, completionToken, token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled(object? completionData, short completionToken, CancellationToken token)
        => SetResult<OperationCanceledExceptionFactory>(completionData, completionToken, token);

    /// <inheritdoc />
    public sealed override bool TrySetException(object? completionData, Exception e)
        => SetResult<ValueSupplier<Exception>>(completionData, completionToken: null, e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetException(short completionToken, Exception e)
        => TrySetException(null, completionToken, e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetException(object? completionData, short completionToken, Exception e)
        => SetResult<ValueSupplier<Exception>>(completionData, completionToken, e);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetResult()
        => TrySetResult(null);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData)
        => SetResult(completionData, completionToken: null, ISupplier<Exception>.NullOrDefault);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(short completionToken)
        => TrySetResult(null, completionToken);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData, short completionToken)
        => SetResult(completionData, completionToken, ISupplier<Exception>.NullOrDefault);

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
    /// <exception cref="InvalidOperationException">The source is in invalid state.</exception>
    public ValueTask CreateTask(TimeSpan timeout, CancellationToken token)
        => Activate(timeout, token) is { } version ? new(this, version) : throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token)
    {
        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        versionAndStatus.Consume(token); // barrier to avoid reordering of result read

        AfterConsumed();
        resultCopy?.Throw();
    }

    /// <inheritdoc />
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
    {
        var resultCopy = result;
        var snapshot = versionAndStatus.VolatileRead(); // barrier to avoid reordering of result read

        if (token != snapshot.Version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        return !snapshot.IsCompleted ? ValueTaskSourceStatus.Pending : resultCopy switch
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
    /// <exception cref="InvalidOperationException">The source is in invalid state.</exception>
    public TaskCompletionSource CreateLinkedTaskCompletionSource(object? userData, TimeSpan timeout, CancellationToken token)
    {
        if (Activate(timeout, token) is not { } version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

        var source = new LinkedTaskCompletionSource(userData);
        source.LinkTo(this, version);
        return source;
    }

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
            source.OnCompleted(
                static state =>
                {
                    Debug.Assert(state is LinkedTaskCompletionSource);

                    Unsafe.As<LinkedTaskCompletionSource>(state).OnCompleted();
                },
                this,
                version,
                ValueTaskSourceOnCompletedFlags.None);
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
}