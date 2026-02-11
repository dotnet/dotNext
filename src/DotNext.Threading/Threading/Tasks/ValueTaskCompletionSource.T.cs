using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

using Runtime;
using Runtime.CompilerServices;

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
public class ValueTaskCompletionSource<T> : ManualResetCompletionSource, IValueTaskSource<T>, IValueTaskSource, IValueTaskFactory<T>
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
        => TrySetResult<DefaultOptions, Result<T>.Ok>(new DefaultOptions(), value);

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="options">The completion options.</param>
    /// <param name="value">The value to be returned to the consumer.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult<TOptions>(TOptions options, T value)
        where TOptions : ICompletionOptions, allows ref struct
        => TrySetResult<TOptions, Result<T>.Ok>(options, value);

    /// <inheritdoc />
    public sealed override bool TrySetException<TOptions>(TOptions options, Exception e)
        => TrySetResult<TOptions, Result<T>.Failure>(options, e);

    private protected sealed override void CompleteAsTimedOut()
        => result = GetTimeoutResult();

    private protected sealed override void CompleteAsCanceled(CancellationToken token)
        => result = GetCancellationResult(token);

    internal bool TrySetResult<TOptions, TResult>(TOptions options, TResult value)
        where TOptions : ICompletionOptions, allows ref struct
        where TResult : struct, IResultMonad<T>
    {
        var completed = TrySetResult(options, value, out var resumable);
        if (resumable)
        {
            NotifyConsumer();
        }

        return completed;
    }

    internal bool TrySetResult<TOptions, TResult>(TOptions options, TResult value, out bool resumable)
        where TOptions : ICompletionOptions, allows ref struct
        where TResult : struct, IResultMonad<T>
    {
        var completed = options.BeginCompletion(this);
        if (completed)
        {
            result = Result<T>.Create(value);
            resumable = options.EndCompletion(this);
        }
        else
        {
            resumable = false;
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
    protected virtual Result<T> GetTimeoutResult() => new(new TimeoutException());

    /// <summary>
    /// Called automatically when cancellation detected.
    /// </summary>
    /// <remarks>
    /// By default, this method assigns <see cref="OperationCanceledException"/> as the task result.
    /// </remarks>
    /// <param name="token">The token representing cancellation reason.</param>
    /// <returns>The result to be assigned to the task.</returns>
    protected virtual Result<T> GetCancellationResult(CancellationToken token) => new(new OperationCanceledException(token));

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
        => new(this, Activate(timeout, token));

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
        Consume(token); // barrier to avoid reordering of result read

        return resultCopy.Value;
    }

    /// <inheritdoc />
    T IValueTaskSource<T>.GetResult(short token) => GetResult(token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token) => GetResult(token);

    /// <inheritdoc cref="IValueTaskSource.GetStatus"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ValueTaskSourceStatus GetStatus(short token)
        => GetStatus<ExceptionProvider>(token, new(in result));

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
        var version = Activate(timeout, token);
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
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ExceptionProvider(ref readonly Result<T> sourceResult) : ISupplier<Exception?>
    {
        private readonly ref readonly Result<T> sourceResult = ref sourceResult;

        Exception? ISupplier<Exception?>.Invoke() => sourceResult.Error;

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }
}