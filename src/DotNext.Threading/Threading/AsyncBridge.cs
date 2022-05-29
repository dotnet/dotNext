using static System.Threading.Timeout;

namespace DotNext.Threading;

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
            throw new ArgumentException(ExceptionMessages.TokenNotCancelable, nameof(token));

        if (token.IsCancellationRequested)
            return completeAsCanceled ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

        CancellationTokenValueTask? result;

        // do not keep long references when treshold reached
        if (instantiatedTasks > maxPoolSize)
            result = new(static t => t.Reset());
        else if (!TokenPool.TryTake(out result))
            result = new(CancellationTokenValueTaskCompletionCallback);

        result.CompleteAsCanceled = completeAsCanceled;
        result.Reset();
        return result.CreateTask(InfiniteTimeSpan, token);
    }

    /// <summary>
    /// Obtains a task that can be used to await handle completion.
    /// </summary>
    /// <param name="handle">The handle to await.</param>
    /// <param name="timeout">The timeout used to await completion.</param>
    /// <returns><see langword="true"/> if handle is signaled; otherwise, <see langword="false"/> if timeout occurred.</returns>
    public static ValueTask<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout)
    {
        if (handle.WaitOne(0))
            return new(true);

        if (timeout == TimeSpan.Zero)
            return new(false);

        WaitHandleValueTask? result;

        // do not keep long references when treshold reached
        if (instantiatedTasks > maxPoolSize)
            result = new(static t => t.Reset());
        else if (!HandlePool.TryTake(out result))
            result = new(WaitHandleTaskCompletionCallback);

        IEquatable<short> token = result.Reset();
        var registration = ThreadPool.RegisterWaitForSingleObject(handle, result.Complete, token, timeout, executeOnlyOnce: true);

        if (result.IsCompleted)
        {
            registration.Unregister(null);
        }
        else
        {
            result.Registration = registration;
        }

        return result.CreateTask(InfiniteTimeSpan, CancellationToken.None);
    }

    /// <summary>
    /// Obtains a task that can be used to await handle completion.
    /// </summary>
    /// <param name="handle">The handle to await.</param>
    /// <returns>The task that will be completed .</returns>
    public static ValueTask<bool> WaitAsync(this WaitHandle handle) => WaitAsync(handle, InfiniteTimeSpan);

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