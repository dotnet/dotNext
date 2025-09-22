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

    private protected sealed override bool CompleteAsTimedOut()
    {
        var dispatchInfo = OnTimeout() is { } e
            ? ExceptionDispatchInfo.Capture(e)
            : null;

        return SetResult(dispatchInfo);
    }

    private protected sealed override bool CompleteAsCanceled(CancellationToken token)
    {
        var dispatchInfo = OnCanceled(token) is { } e
            ? ExceptionDispatchInfo.Capture(e)
            : null;

        return SetResult(dispatchInfo);
    }

    /// <inheritdoc />
    protected override void CleanUp() => result = null;

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

    /// <summary>
    /// Tries to set the result of this source without resuming the <see cref="ValueTask"/> consumer.
    /// </summary>
    /// <param name="completionData">The completion data to be assigned to <see cref="ManualResetCompletionSource.CompletionData"/> property.</param>
    /// <param name="completionToken">The optional completion token.</param>
    /// <param name="dispatchInfo">The exception to be stored as the result of <see cref="ValueTask"/>.</param>
    /// <param name="resumable">
    /// <see langword="true"/> if <see cref="ManualResetCompletionSource.Resume()"/> needs to be called to resume
    /// the consumer of <see cref="ValueTask"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if this source is completed successfully;
    /// <see langword="false"/> if this source was completed previously.
    /// </returns>
    protected internal bool TrySetResult(object? completionData, short? completionToken, ExceptionDispatchInfo? dispatchInfo, out bool resumable)
    {
        bool completed;
        lock (SyncRoot)
        {
            resumable = (completed = versionAndStatus.CanBeCompleted(completionToken)) && SetResult(dispatchInfo, completionData);
        }

        return completed;
    }

    private bool TrySetResult(object? completionData, short? completionToken, ExceptionDispatchInfo? dispatchInfo)
    {
        var completed = TrySetResult(completionData, completionToken, dispatchInfo, out var resumable);
        if (resumable)
        {
            Resume();
        }

        return completed;
    }

    private bool SetResult(ExceptionDispatchInfo? dispatchInfo, object? completionData = null)
    {
        AssertLocked();

        result = dispatchInfo;
        return SetResult(completionData);
    }

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled(object? completionData, short completionToken, CancellationToken token)
        => TrySetException(completionData, completionToken, new OperationCanceledException(token));

    /// <inheritdoc />
    public sealed override bool TrySetException(object? completionData, Exception e)
        => TrySetResult(completionData, completionToken: null, ExceptionDispatchInfo.Capture(e));

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetException(object? completionData, short completionToken, Exception e)
        => TrySetResult(completionData, completionToken, ExceptionDispatchInfo.Capture(e));

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetResult()
        => TrySetResult(null);

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData)
        => TrySetResult(completionData, completionToken: null, dispatchInfo: null);

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData, short completionToken)
        => TrySetResult(completionData, completionToken, dispatchInfo: null);

    /// <summary>
    /// Creates a fresh task linked with this source.
    /// </summary>
    /// <remarks>
    /// This method must be called after <see cref="ManualResetCompletionSource.Reset()"/>.
    /// </remarks>
    /// <param name="timeout">The timeout associated with the task.</param>
    /// <param name="token">The cancellation token that can be used to cancel the task.</param>
    /// <returns>A fresh uncompleted task.</returns>
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
        => GetStatus(token, result?.SourceException);

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