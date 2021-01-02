using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Allows to turn <see cref="WaitHandle"/> and <see cref="CancellationToken"/> into task.
    /// </summary>
    public static class AsyncBridge
    {
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
#if NETSTANDARD2_1
                return completeAsCanceled ? new ValueTask(Task.FromCanceled(token)) : new ValueTask();
#else
                return completeAsCanceled ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;
#endif

            return new CancellationTokenFuture(completeAsCanceled, token).AsTask();
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
                return new ValueTask<bool>(true);

            if (timeout == TimeSpan.Zero)
                return new ValueTask<bool>(false);

            return new WaitHandleFuture(handle, timeout).AsTask();
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <returns>The task that will be completed .</returns>
        public static ValueTask<bool> WaitAsync(this WaitHandle handle) => WaitAsync(handle, InfiniteTimeSpan);
    }
}
