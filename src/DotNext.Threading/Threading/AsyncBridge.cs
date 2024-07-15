using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using ManualResetCompletionSource = Tasks.ManualResetCompletionSource;

/// <summary>
/// Allows to turn <see cref="WaitHandle"/> and <see cref="CancellationToken"/> into task.
/// </summary>
public static partial class AsyncBridge
{
    private static volatile int instantiatedTasks;
    private static int maxPoolSize = Environment.ProcessorCount * 2;

    /// <summary>
    /// Obtains a task that can be used to await token cancellation.
    /// </summary>
    /// <param name="token">The token to be converted into task.</param>
    /// <param name="completeAsCanceled"><see langword="true"/> to complete task in <see cref="TaskStatus.Canceled"/> state; <see langword="false"/> to complete task in <see cref="TaskStatus.RanToCompletion"/> state.</param>
    /// <returns>A task representing token state.</returns>
    /// <exception cref="ArgumentException"><paramref name="token"/> doesn't support cancellation.</exception>
    public static ValueTask WaitAsync(this CancellationToken token, bool completeAsCanceled = false)
    {
        if (!token.CanBeCanceled)
            return ValueTask.FromException(new ArgumentException(ExceptionMessages.TokenNotCancelable, nameof(token)));

        if (token.IsCancellationRequested)
            return completeAsCanceled ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

        CancellationTokenValueTask? result;

        // do not keep long references when limit is reached
        if (instantiatedTasks > maxPoolSize)
        {
            if (completeAsCanceled)
                return new(Task.Delay(InfiniteTimeSpan, token));

            result = new(Reset);
        }
        else if (!TokenPool.TryTake(out result))
        {
            result = new(CancellationTokenValueTaskCompletionCallback);
        }

        result.CompleteAsCanceled = completeAsCanceled;
        result.Reset();
        return result.CreateTask(InfiniteTimeSpan, token);
    }

    private static CancellationTokenCompletionSource GetCompletionSource(ReadOnlySpan<CancellationToken> tokens) => tokens switch
    {
        [] => throw new InvalidOperationException(),
        [var token] => new CancellationTokenCompletionSource1(token),
        [var token1, var token2] => new CancellationTokenCompletionSource2(token1, token2),
        _ => new CancellationTokenCompletionSourceN(tokens),
    };

    /// <summary>
    /// Creates a task that will complete when any of the supplied tokens have canceled.
    /// </summary>
    /// <param name="tokens">The tokens to wait on for cancellation.</param>
    /// <returns>The canceled token.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="tokens"/> is empty.</exception>
    public static Task<CancellationToken> WaitAnyAsync(this ReadOnlySpan<CancellationToken> tokens)
    {
        Task<CancellationToken> result;
        try
        {
            result = GetCompletionSource(tokens).Task;
        }
        catch (Exception e)
        {
            result = Task.FromException<CancellationToken>(e);
        }

        return result;
    }

    /// <summary>
    /// Creates a task that will complete when any of the supplied tokens have canceled.
    /// </summary>
    /// <param name="tokens">The tokens to wait on for cancellation.</param>
    /// <param name="interruption">An interruption procedure than can be used to turn the returned task into the failed state.</param>
    /// <returns>The canceled token.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="tokens"/> is empty.</exception>
    /// <exception cref="PendingTaskInterruptedException">The returned task is interrupted by <paramref name="interruption"/> procedure.</exception>
    public static Task<CancellationToken> WaitAnyAsync(this ReadOnlySpan<CancellationToken> tokens, out Func<object?, bool> interruption)
    {
        Task<CancellationToken> result;
        try
        {
            var source = GetCompletionSource(tokens);
            result = source.Task;
            interruption = source.TryInterrupt;
        }
        catch (Exception e)
        {
            result = Task.FromException<CancellationToken>(e);
            interruption = static _ => false;
        }

        return result;
    }

    private static WaitHandleValueTask GetCompletionSource(WaitHandle handle, TimeSpan timeout)
    {
        WaitHandleValueTask? result;

        // do not keep long references when limit is reached
        if (instantiatedTasks > maxPoolSize)
            result = new(Reset);
        else if (!HandlePool.TryTake(out result))
            result = new(WaitHandleTaskCompletionCallback);

        var token = result.Reset();
        var registration = ThreadPool.UnsafeRegisterWaitForSingleObject(
            handle,
            Complete,
            new Tuple<WaitHandleValueTask, short>(result, token),
            timeout,
            executeOnlyOnce: true);

        if (result.IsCompleted)
        {
            registration.Unregister(null);
        }
        else
        {
            result.Registration = registration;
        }

        return result;

        static void Complete(object? state, bool timedOut)
        {
            Debug.Assert(state is Tuple<WaitHandleValueTask, short>);

            var (source, token) = Unsafe.As<Tuple<WaitHandleValueTask, short>>(state);
            source.TrySetResult(token, timedOut is false);
        }
    }

    /// <summary>
    /// Obtains a task that can be used to await handle completion.
    /// </summary>
    /// <param name="handle">The handle to await.</param>
    /// <param name="timeout">The timeout used to await completion.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if handle is signaled; otherwise, <see langword="false"/> if timeout occurred.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask<bool> result;
        if (handle.WaitOne(0))
        {
            result = new(true);
        }
        else if (timeout == TimeSpan.Zero)
        {
            result = new(false);
        }
        else
        {
            result = GetCompletionSource(handle, timeout).CreateTask(InfiniteTimeSpan, token);
        }

        return result;
    }

    private static void Reset(ManualResetCompletionSource source) => source.Reset();

    /// <summary>
    /// Obtains a task that can be used to await handle completion.
    /// </summary>
    /// <param name="handle">The handle to await.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task that will be completed .</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WaitAsync(this WaitHandle handle, CancellationToken token = default)
        => handle.WaitOne(0)
            ? ValueTask.CompletedTask
            : GetCompletionSource(handle, InfiniteTimeSpan)
                .As<ISupplier<TimeSpan, CancellationToken, ValueTask>>()
                .Invoke(InfiniteTimeSpan, token);
    
    /// <summary>
    /// Returns a cancellation token that gets signaled when the task completes.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <returns>The token that represents completion state of the task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    public static CancellationToken AsCancellationToken(this Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        CancellationToken result;
        if (task.IsCompleted)
        {
            result = new(canceled: true);
        }
        else
        {
            task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(new TaskToCancellationTokenCallback(out result).CancelAndDispose);
        }

        return result;
    }

    /// <summary>
    /// Returns a cancellation token that gets signaled when the task completes.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="disposeTokenSource">
    /// A delegate that can be used to destroy the source of the returned token if no longer needed.
    /// It returns <see langword="true"/> if token was not canceled by the task; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>The token that represents completion state of the task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    public static CancellationToken AsCancellationToken(this Task task, out Func<bool> disposeTokenSource)
    {
        ArgumentNullException.ThrowIfNull(task);

        CancellationToken result;
        if (task.IsCompleted)
        {
            result = new(canceled: true);
            disposeTokenSource = Func.Constant(false);
        }
        else
        {
            var callback = new TaskToCancellationTokenCallback(out result);
            disposeTokenSource = callback.TryDispose;
            task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(callback.CancelAndDispose);
        }

        return result;
    }
    
    /// <summary>
    /// Gets or sets the capacity of the internal pool used to create awaitable tasks returned
    /// from the public methods in this class.
    /// </summary>
    public static int MaxPoolSize
    {
        get => maxPoolSize;
        set => maxPoolSize = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}