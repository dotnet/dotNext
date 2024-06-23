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
    /// Gets or sets the capacity of the internal pool used to create awaitable tasks returned
    /// from the public methods in this class.
    /// </summary>
    public static int MaxPoolSize
    {
        get => maxPoolSize;
        set => maxPoolSize = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}