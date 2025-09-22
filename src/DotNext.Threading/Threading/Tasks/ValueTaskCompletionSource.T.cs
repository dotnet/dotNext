using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents the producer side of <see cref="ValueTask{T}"/>.
/// </summary>
/// <remarks>
/// In contrast to <see cref="TaskCompletionSource{T}"/>, this
/// source is resettable.
/// From performance point of view, the type offers minimal or zero memory allocation
/// for the task itself (excluding continuations). See <see cref="CreateTask(TimeSpan, CancellationToken)"/>
/// for more information.
/// The instance of this type typically used in combination with object pool pattern because
/// the instance can be reused for multiple tasks.
/// <see cref="ManualResetCompletionSource.AfterConsumed()"/> method allows to capture the point in
/// time when the source can be reused, e.g. returned to the pool.
/// </remarks>
/// <typeparam name="T">>The type the task result.</typeparam>
/// <seealso cref="ValueTaskCompletionSource"/>
public class ValueTaskCompletionSource<T> : ManualResetCompletionSource, IValueTaskSource<T>, IValueTaskSource, ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<T>>
{
    private Result<T> result;

    /// <summary>
    /// Initializes a new completion source.
    /// </summary>
    /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
    public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(T value)
        => TrySetResult(null, value);

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData, T value)
        => TrySetResult(completionData, completionToken: null, new Result<T>(value));

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult(object? completionData, short completionToken, T value)
        => TrySetResult(completionData, completionToken, new Result<T>(value));

    /// <inheritdoc />
    public sealed override bool TrySetException(object? completionData, Exception e)
        => TrySetResult(completionData, completionToken: null, new Result<T>(e));

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetException(object? completionData, short completionToken, Exception e)
        => TrySetResult(completionData, completionToken, new Result<T>(e));

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetCanceled(object? completionData, short completionToken, CancellationToken token)
        => TrySetException(completionData, completionToken, new OperationCanceledException(token));

    private protected sealed override bool CompleteAsTimedOut()
        => SetResult(OnTimeout());

    private protected sealed override bool CompleteAsCanceled(CancellationToken token)
        => SetResult(OnCanceled(token));

    private bool TrySetResult(object? completionData, short? completionToken, in Result<T> result)
    {
        var completed = TrySetResult(completionData, completionToken, in result, out var resumable);
        if (resumable)
        {
            Resume();
        }

        return completed;
    }

    private bool SetResult(in Result<T> result, object? completionData = null)
    {
        AssertLocked();

        this.result = result;
        return SetResult(completionData);
    }

    /// <summary>
    /// Tries to set the result of this source without resuming the <see cref="ValueTask{TResult}"/> consumer.
    /// </summary>
    /// <param name="completionData">The completion data to be assigned to <see cref="ManualResetCompletionSource.CompletionData"/> property.</param>
    /// <param name="completionToken">The optional completion token.</param>
    /// <param name="result">The result to be stored as the result of <see cref="ValueTask{TResult}"/>.</param>
    /// <param name="resumable">
    /// <see langword="true"/> if <see cref="ManualResetCompletionSource.Resume()"/> needs to be called to resume
    /// the consumer of <see cref="ValueTask{TResult}"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if this source is completed successfully;
    /// <see langword="false"/> if this source was completed previously.
    /// </returns>
    protected internal bool TrySetResult(object? completionData, short? completionToken, in Result<T> result, out bool resumable)
    {
        bool completed;
        lock (SyncRoot)
        {
            resumable = (completed = versionAndStatus.CanBeCompleted(completionToken)) && SetResult(in result, completionData);
        }

        return completed;
    }

    /// <inheritdoc />
    protected override void CleanUp() => result = default;

    /// <summary>
    /// Called automatically when timeout detected.
    /// </summary>
    /// <remarks>
    /// By default, this method assigns <see cref="TimeoutException"/> as the task result.
    /// </remarks>
    /// <returns>The result to be assigned to the task.</returns>
    protected virtual Result<T> OnTimeout() => new(new TimeoutException());

    /// <summary>
    /// Called automatically when cancellation detected.
    /// </summary>
    /// <remarks>
    /// By default, this method assigns <see cref="OperationCanceledException"/> as the task result.
    /// </remarks>
    /// <param name="token">The token representing cancellation reason.</param>
    /// <returns>The result to be assigned to the task.</returns>
    protected virtual Result<T> OnCanceled(CancellationToken token) => new(new OperationCanceledException(token));

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
    public ValueTask<T> CreateTask(TimeSpan timeout, CancellationToken token)
        => Activate(timeout, token) is { } version ? new(this, version) : throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    /// <inheritdoc />
    ValueTask<T> ISupplier<TimeSpan, CancellationToken, ValueTask<T>>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
        => Activate(timeout, token) is { } version ? new(this, version) : throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    private T GetResult(short token)
    {
        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        versionAndStatus.Consume(token); // barrier to avoid reordering of result read

        AfterConsumed();
        return resultCopy.Value;
    }

    /// <inheritdoc />
    T IValueTaskSource<T>.GetResult(short token) => GetResult(token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token) => GetResult(token);

    /// <inheritdoc cref="IValueTaskSource.GetStatus"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ValueTaskSourceStatus GetStatus(short token)
        => GetStatus(token, result.Error);

    /// <summary>
    /// Creates a linked <see cref="TaskCompletionSource{TResult}"/> that can be used cooperatively to
    /// complete the task.
    /// </summary>
    /// <param name="userData">The custom data to be associated with the current version of the task.</param>
    /// <param name="timeout">The timeout associated with the task.</param>
    /// <param name="token">The cancellation token that can be used to cancel the task.</param>
    /// <returns>A linked <see cref="TaskCompletionSource{TResult}"/>.</returns>
    /// <exception cref="InvalidOperationException">The source is in invalid state.</exception>
    public TaskCompletionSource<T> CreateLinkedTaskCompletionSource(object? userData, TimeSpan timeout, CancellationToken token)
    {
        if (Activate(timeout, token) is not { } version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

        var source = new LinkedTaskCompletionSource(userData);
        source.LinkTo(this, version);
        return source;
    }

    private sealed class LinkedTaskCompletionSource : TaskCompletionSource<T>
    {
        private IValueTaskSource<T>? source;
        private short version;

        internal LinkedTaskCompletionSource(object? state)
            : base(state, TaskCreationOptions.None)
        {
        }

        internal void LinkTo(IValueTaskSource<T> source, short version)
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
                    TrySetResult(source.GetResult(version));
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