using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents the producer side of <see cref="ValueTask{T}"/>.
/// </summary>
/// <remarks>
/// In constrast to <see cref="TaskCompletionSource{T}"/>, this
/// source is resettable.
/// From performance point of view, the type offers minimal or zero memory allocation
/// for the task itself (excluding continuations). See <see cref="CreateTask(TimeSpan, CancellationToken)"/>
/// for more information.
/// The instance of this type typically used in combination with object pool pattern because
/// the instance can be reused for multiple tasks.
/// <see cref="ManualResetCompletionSource.AfterConsumed"/> method allows to capture the point in
/// time when the source can be reused, e.g. returned to the pool.
/// </remarks>
/// <typeparam name="T">>The type the task result.</typeparam>
/// <seealso cref="ValueTaskCompletionSource"/>
public class ValueTaskCompletionSource<T> : ManualResetCompletionSource, IValueTaskSource<T>, IValueTaskSource, ISupplier<TimeSpan, CancellationToken, ValueTask<T>>, ISupplier<TimeSpan, CancellationToken, ValueTask>
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

    private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource<T>);

    private static Result<T> FromCanceled(CancellationToken token)
        => new(new OperationCanceledException(token));

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetResult(T value)
        => TrySetResult(&Result.FromValue, value);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetResult(short completionToken, T value)
        => TrySetResult(completionToken, &Result.FromValue, value);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public sealed override unsafe bool TrySetException(Exception e)
        => TrySetResult(&Result.FromException<T>, e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="e">The exception to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetException(short completionToken, Exception e)
        => TrySetResult(completionToken, &Result.FromException<T>, e);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public sealed override unsafe bool TrySetCanceled(CancellationToken token)
        => TrySetResult(&FromCanceled, token);

    /// <summary>
    /// Attempts to complete the task unsuccessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="token">The canceled token.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetCanceled(short completionToken, CancellationToken token)
        => TrySetResult(completionToken, &FromCanceled, token);

    private protected sealed override void CompleteAsTimedOut()
        => SetResult(OnTimeout());

    private protected sealed override void CompleteAsCanceled(CancellationToken token)
        => SetResult(OnCanceled(token));

    private unsafe bool TrySetResult<TArg>(delegate*<TArg, Result<T>> func, TArg arg)
    {
        Debug.Assert(func != null);

        bool result;
        if (IsCompleted)
        {
            result = false;
        }
        else
        {
            lock (SyncRoot)
            {
                if (IsCompleted)
                {
                    result = false;
                }
                else
                {
                    SetResult(func(arg));
                    result = true;
                }
            }
        }

        return result;
    }

    private unsafe bool TrySetResult<TArg>(short completionToken, delegate*<TArg, Result<T>> func, TArg arg)
    {
        Debug.Assert(func != null);

        bool result;
        if (IsCompleted)
        {
            result = false;
        }
        else
        {
            lock (SyncRoot)
            {
                if (IsCompleted || completionToken != version)
                {
                    result = false;
                }
                else
                {
                    SetResult(func(arg));
                    result = true;
                }
            }
        }

        return result;
    }

    private void SetResult(Result<T> result)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        StopTrackingCancellation();
        this.result = result;
        IsCompleted = true;
        InvokeContinuation();
    }

    private protected sealed override void ResetCore()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        base.ResetCore();
        result = default;
    }

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
    /// <returns>A fresh incompleted task.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than zero but not equals to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</exception>
    public ValueTask<T> CreateTask(TimeSpan timeout, CancellationToken token)
    {
        PrepareTask(timeout, token);
        return new(this, version);
    }

    /// <inheritdoc />
    ValueTask<T> ISupplier<TimeSpan, CancellationToken, ValueTask<T>>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
    {
        PrepareTask(timeout, token);
        return new(this, version);
    }

    private T GetResult(short token)
    {
        if (!IsCompleted || token != version)
            throw new InvalidOperationException();

        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        Thread.MemoryBarrier();

        if (IsDerived)
            QueueAfterConsumed();

        return resultCopy.Value;
    }

    /// <inheritdoc />
    T IValueTaskSource<T>.GetResult(short token) => GetResult(token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token) => GetResult(token);

    private ValueTaskSourceStatus GetStatus(short token)
    {
        if (token != version)
            throw new InvalidOperationException();

        if (!IsCompleted)
            return ValueTaskSourceStatus.Pending;

        var error = result.Error;
        if (error is null)
            return ValueTaskSourceStatus.Succeeded;

        return error is OperationCanceledException ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted;
    }

    /// <inheritdoc />
    ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token) => GetStatus(token);

    /// <inheritdoc />
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => GetStatus(token);

    /// <inheritdoc />
    void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => OnCompleted(continuation, state, token, flags);

    /// <inheritdoc />
    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => OnCompleted(continuation, state, token, flags);
}