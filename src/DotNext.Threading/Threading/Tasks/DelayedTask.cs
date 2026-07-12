using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents a task with delayed completion.
/// </summary>
public abstract class DelayedTask
{
    private protected const uint InitialState = 0U;
    private protected const uint DelayState = 1U;

    private protected readonly CancellationToken token; // cached token to avoid ObjectDisposedException
    private volatile CancellationTokenSource? tokenSource;
    private protected uint state;
    private Action? continuation;

    private protected DelayedTask(CancellationToken token)
        => this.token = (tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)).Token;

    /// <summary>
    /// Gets delayed task.
    /// </summary>
    /// <seealso cref="DelayedTaskCanceledException"/>
    public abstract Task Task { get; }

    /// <summary>
    /// Cancels scheduled task.
    /// </summary>
    public void Cancel()
    {
        if (Interlocked.Exchange(ref tokenSource, null) is { } cts)
        {
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    private protected void Cleanup() => Interlocked.Exchange(ref tokenSource, null)?.Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void GetResultAndClear(ref ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter)
    {
        var awaiterCopy = awaiter;
        awaiter = default;
        awaiterCopy.GetResult();
    }

    /// <summary>
    /// Gets an awaiter used to await this task.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();

    /// <summary>
    /// Configures an awaiter used to await this task.
    /// </summary>
    /// <param name="continueOnCapturedContext">
    /// <see langword="true"/> to attempt to marshal the continuation back to the original context captured;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>An awaiter instance.</returns>
    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => Task.ConfigureAwait(continueOnCapturedContext);

    private protected abstract void SetException(Exception e);

    private protected abstract void AdvanceStateMachine();

    private protected void Await<TAwaiter>(ref TAwaiter awaiter)
        where TAwaiter : struct, INotifyCompletion
        => awaiter.OnCompleted(continuation ??= MoveNext);

    private void MoveNext()
    {
        try
        {
            AdvanceStateMachine();
        }
        catch (Exception e)
        {
            Cleanup();

            if (state is DelayState && e is OperationCanceledException canceledEx)
            {
                e = new DelayedTaskCanceledException(canceledEx);

                if (canceledEx.StackTrace is { Length: > 0 } stackTrace)
                    ExceptionDispatchInfo.SetRemoteStackTrace(e, stackTrace);
                else
                    ExceptionDispatchInfo.SetCurrentStackTrace(e);
            }

            SetException(e);
        }
    }

    private protected static void Start(DelayedTask stateMachine)
        => stateMachine.MoveNext();
}

/// <summary>
/// Represents a task with delayed completion.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by this task.</typeparam>
public abstract class DelayedTask<TResult> : DelayedTask
{
    private protected DelayedTask(CancellationToken token)
        : base(token)
    {
    }

    /// <inheritdoc />
    public abstract override Task<TResult> Task { get; }

    /// <summary>
    /// Gets an awaiter used to await this task.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    public new TaskAwaiter<TResult> GetAwaiter() => Task.GetAwaiter();

    /// <summary>
    /// Configures an awaiter used to await this task.
    /// </summary>
    /// <param name="continueOnCapturedContext">
    /// <see langword="true"/> to attempt to marshal the continuation back to the original context captured;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>An awaiter instance.</returns>
    public new ConfiguredTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext)
        => Task.ConfigureAwait(continueOnCapturedContext);
}