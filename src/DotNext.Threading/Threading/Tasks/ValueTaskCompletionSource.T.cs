using System.Runtime.CompilerServices;
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
public class ValueTaskCompletionSource<T> : ManualResetCompletionSource, IValueTaskSource<T>, IValueTaskSource, ISupplier<TimeSpan, CancellationToken, ValueTask>, ISupplier<TimeSpan, CancellationToken, ValueTask<T>>
{
    private sealed class LinkedTaskCompletionSource : TaskCompletionSource<T>
    {
        private static readonly Action<object?> CompletionCallback = OnCompleted;

        private static void OnCompleted(object? state)
        {
            Debug.Assert(state is LinkedTaskCompletionSource);

            Unsafe.As<LinkedTaskCompletionSource>(state).OnCompleted();
        }

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
            source.OnCompleted(CompletionCallback, this, version, ValueTaskSourceOnCompletedFlags.None);
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

    private Result<T> result;

    /// <summary>
    /// Initializes a new completion source.
    /// </summary>
    /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
    public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    private static Result<T> FromCanceled(CancellationToken token)
        => new(new OperationCanceledException(token));

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetResult(T value)
        => TrySetResult(null, value);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetResult(object? completionData, T value)
        => TrySetResult(&Result.FromValue, value, completionData);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetResult(short completionToken, T value)
        => TrySetResult(null, completionToken, value);

    /// <summary>
    /// Attempts to complete the task sucessfully.
    /// </summary>
    /// <param name="completionData">The data to be saved in <see cref="ManualResetCompletionSource.CompletionData"/> property that can be accessed from within <see cref="ManualResetCompletionSource.AfterConsumed"/> method.</param>
    /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public unsafe bool TrySetResult(object? completionData, short completionToken, T value)
        => TrySetResult(&Result.FromValue, value, completionData, completionToken);

    /// <inheritdoc />
    public sealed override unsafe bool TrySetException(object? completionData, Exception e)
        => TrySetResult(&Result.FromException<T>, e, completionData);

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
    public unsafe bool TrySetException(object? completionData, short completionToken, Exception e)
        => TrySetResult(&Result.FromException<T>, e, completionData, completionToken);

    /// <inheritdoc />
    public sealed override unsafe bool TrySetCanceled(object? completionData, CancellationToken token)
        => TrySetResult(&FromCanceled, token, completionData);

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
    public unsafe bool TrySetCanceled(object? completionData, short completionToken, CancellationToken token)
        => TrySetResult(&FromCanceled, token, completionData, completionToken);

    private protected sealed override void CompleteAsTimedOut()
        => SetResult(OnTimeout());

    private protected sealed override void CompleteAsCanceled(CancellationToken token)
        => SetResult(OnCanceled(token));

    private unsafe bool TrySetResult<TArg>(delegate*<TArg, Result<T>> func, TArg arg, object? completionData, short? completionToken = null)
    {
        Debug.Assert(func != null);

        bool result;
        if (result = CanBeCompleted)
        {
            lock (SyncRoot)
            {
                if (result = CanBeCompleted && completionToken.GetValueOrDefault(version) == version)
                    SetResult(func(arg), completionData);
            }
        }

        return result;
    }

    private void SetResult(Result<T> result, object? completionData = null)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        StopTrackingCancellation();
        this.result = result;
        OnCompleted(completionData);
        InvokeContinuation();
    }

    private protected override void ResetCore()
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
    /// <exception cref="InvalidOperationException">The source is in invalid state.</exception>
    public ValueTask<T> CreateTask(TimeSpan timeout, CancellationToken token)
        => PrepareTask(timeout, token) ? new(this, version) : throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    /// <inheritdoc />
    ValueTask<T> ISupplier<TimeSpan, CancellationToken, ValueTask<T>>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
        => PrepareTask(timeout, token) ? new(this, version) : throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

    private T GetResult(short token)
    {
        if (Status != ManualResetCompletionSourceStatus.WaitForConsumption)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

        if (token != version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        Thread.MemoryBarrier();

        OnConsumed<ValueTaskCompletionSource<T>>();
        return resultCopy.Value;
    }

    /// <inheritdoc />
    T IValueTaskSource<T>.GetResult(short token) => GetResult(token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token) => GetResult(token);

    private ValueTaskSourceStatus GetStatus(short token)
    {
        if (token != version)
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

        return !IsCompleted ? ValueTaskSourceStatus.Pending : result.Error switch
        {
            null => ValueTaskSourceStatus.Succeeded,
            OperationCanceledException => ValueTaskSourceStatus.Canceled,
            _ => ValueTaskSourceStatus.Faulted,
        };
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
        if (!PrepareTask(timeout, token))
            throw new InvalidOperationException(ExceptionMessages.InvalidSourceState);

        var source = new LinkedTaskCompletionSource(userData);
        source.LinkTo(this, version);
        return source;
    }
}