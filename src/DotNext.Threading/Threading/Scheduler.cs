using static System.Threading.Timeout;

namespace DotNext.Threading;

/// <summary>
/// Represents timer-based scheduler.
/// </summary>
public static partial class Scheduler
{
    /// <summary>
    /// Schedules the specific action to be execute once after the specified delay.
    /// </summary>
    /// <typeparam name="TArgs">The type of arguments to be passed to the callback.</typeparam>
    /// <param name="callback">The callback to be executed after the specified delay.</param>
    /// <param name="args">The arguments to be passed to the callback.</param>
    /// <param name="delay">The amount of time used to delay callback execution.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing delayed execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is less than <see cref="TimeSpan.Zero"/> and not equal to <see cref="InfiniteTimeSpan"/>.</exception>
    public static DelayedTask ScheduleAsync<TArgs>(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, TimeSpan delay, CancellationToken token = default)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        if (delay < TimeSpan.Zero && delay != InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(delay));

        if (token.IsCancellationRequested)
            return new(token);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        return new(ExecuteAsync(), cts);

        async Task ExecuteAsync()
        {
            using (cts)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                await callback(args, cts.Token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Schedules the specific action to be execute once after the specified delay.
    /// </summary>
    /// <typeparam name="TArgs">The type of arguments to be passed to the callback.</typeparam>
    /// <typeparam name="TResult">The type of the result to returned from the callback.</typeparam>
    /// <param name="callback">The callback to be executed after the specified delay.</param>
    /// <param name="args">The arguments to be passed to the callback.</param>
    /// <param name="delay">The amount of time used to delay callback execution.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing delayed execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is less than <see cref="TimeSpan.Zero"/> and not equal to <see cref="InfiniteTimeSpan"/>.</exception>
    public static DelayedTask<TResult> ScheduleAsync<TArgs, TResult>(Func<TArgs, CancellationToken, ValueTask<TResult>> callback, TArgs args, TimeSpan delay, CancellationToken token = default)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        if (delay < TimeSpan.Zero && delay != InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(delay));

        if (token.IsCancellationRequested)
            return new(token);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        return new(ExecuteAsync(), cts);

        async Task<TResult> ExecuteAsync()
        {
            using (cts)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                return await callback(args, cts.Token).ConfigureAwait(false);
            }
        }
    }
}