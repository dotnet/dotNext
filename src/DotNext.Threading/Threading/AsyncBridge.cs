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
        public static CancellationTokenFuture WaitAsync(this CancellationToken token, bool completeAsCanceled = false)
        {
            if (token.IsCancellationRequested)
                return completeAsCanceled ? CancellationTokenFuture.Canceled : CancellationTokenFuture.Completed;
            if (token.CanBeCanceled)
                return new CancellationTokenFuture(completeAsCanceled, ref token);
            throw new ArgumentException(ExceptionMessages.TokenNotCancelable, nameof(token));
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <param name="timeout">The timeout used to await completion.</param>
        /// <returns><see langword="true"/> if handle is signaled; otherwise, <see langword="false"/> if timeout occurred.</returns>
        public static WaitHandleFuture WaitAsync(this WaitHandle handle, TimeSpan timeout)
        {
            if (handle.WaitOne(0))
                return WaitHandleFuture.Successful;
            else if (timeout == TimeSpan.Zero)
                return WaitHandleFuture.TimedOut;
            else
                return new WaitHandleFuture(handle, timeout);
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <returns>The task that will be completed .</returns>
        public static WaitHandleFuture WaitAsync(this WaitHandle handle) => WaitAsync(handle, InfiniteTimeSpan);
    }
}
