using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

using Runtime;
using Runtime.CompilerServices;

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

    private protected sealed override void CompleteAsTimedOut() => result = GetTimeoutException() is { } e
        ? ExceptionDispatchInfo.Capture(e)
        : null;

    private protected sealed override void CompleteAsCanceled(CancellationToken token) => result = GetCancellationException(token) is { } e
        ? ExceptionDispatchInfo.Capture(e)
        : null;

    /// <inheritdoc />
    protected override void CleanUp() => result = null;

    /// <summary>
    /// Called automatically when timeout detected.
    /// </summary>
    /// <remarks>
    /// By default, this method returns <see cref="TimeoutException"/> as the task result.
    /// </remarks>
    /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
    protected virtual Exception? GetTimeoutException() => new TimeoutException();

    /// <summary>
    /// Called automatically when cancellation detected.
    /// </summary>
    /// <remarks>
    /// By default, this method returns <see cref="OperationCanceledException"/> as the task result.
    /// </remarks>
    /// <param name="token">The token representing cancellation reason.</param>
    /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
    protected virtual Exception? GetCancellationException(CancellationToken token) => new OperationCanceledException(token);

    private bool TrySetResult<TOptions>(TOptions options, ExceptionDispatchInfo? dispatchInfo, out bool resumable)
        where TOptions : ICompletionOptions, allows ref struct
    {
        var completed = options.BeginCompletion(this);
        if (completed)
        {
            result = dispatchInfo;
            resumable = options.EndCompletion(this);
        }
        else
        {
            resumable = false;
        }

        return completed;
    }

    private bool TrySetResult<TOptions>(TOptions options, ExceptionDispatchInfo? dispatchInfo)
        where TOptions : ICompletionOptions, allows ref struct
    {
        var completed = TrySetResult(options, dispatchInfo, out var resumable);
        if (resumable)
        {
            NotifyConsumer();
        }

        return completed;
    }

    /// <inheritdoc />
    public sealed override bool TrySetException<TOptions>(TOptions options, Exception e)
        => TrySetResult(options, ExceptionDispatchInfo.Capture(e));

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult()
        => TrySetResult(new DefaultOptions());

    /// <summary>
    /// Attempts to complete the task successfully.
    /// </summary>
    /// <param name="options">The completion options.</param>
    /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
    public bool TrySetResult<TOptions>(TOptions options)
        where TOptions : ICompletionOptions, allows ref struct
        => TrySetResult(options, dispatchInfo: null);

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
        => new(this, Activate(timeout, token));

    /// <inheritdoc />
    ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
        => CreateTask(timeout, token);

    /// <inheritdoc />
    void IValueTaskSource.GetResult(short token)
    {
        // ensure that instance field access before returning to the pool to avoid
        // concurrency with Reset()
        var resultCopy = result;
        Consume(token); // barrier to avoid reordering of result read

        resultCopy?.Throw();
    }

    /// <inheritdoc />
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => GetStatus<ExceptionProvider>(token, new(in result));

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
        var version = Activate(timeout, token);
        var source = new LinkedTaskCompletionSource(userData);
        source.LinkTo(this, version);
        return source;
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

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ExceptionProvider(ref readonly ExceptionDispatchInfo? dispatchInfo) : ISupplier<Exception?>
    {
        private readonly ref readonly ExceptionDispatchInfo? dispatchInfo = ref dispatchInfo;

        Exception? ISupplier<Exception?>.Invoke() => dispatchInfo?.SourceException;

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }
}