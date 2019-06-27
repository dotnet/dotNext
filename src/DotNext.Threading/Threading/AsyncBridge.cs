using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using AwaitableWaitHandle = Runtime.CompilerServices.AwaitableWaitHandle;

    /// <summary>
    /// Allows to turn <see cref="WaitHandle"/> and <see cref="CancellationToken"/> into task.
    /// </summary>
    public static class AsyncBridge
    {
        private static readonly Action<Task> IgnoreCancellationContinuation = task => { };

        /// <summary>
        /// Obtains a task that can be used to await token cancellation.
        /// </summary>
        /// <param name="token">The token to be converted into task.</param>
        /// <param name="completeAsCanceled"><see langword="true"/> to complete task in <see cref="TaskStatus.Canceled"/> state; <see langword="false"/> to complete task in <see cref="TaskStatus.RanToCompletion"/> state.</param>
        /// <returns>A task representing token state.</returns>
        /// <exception cref="ArgumentException"><paramref name="token"/> doesn't support cancellation.</exception>
        public static Task WaitAsync(this CancellationToken token, bool completeAsCanceled = false)
        {
            if (!token.CanBeCanceled)
                throw new ArgumentException(ExceptionMessages.TokenNotCancelable, nameof(token));
            var task = Task.Delay(Infinite, token);
            return completeAsCanceled ? task : task.ContinueWith(IgnoreCancellationContinuation, default, TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <param name="timeout">The timeout used to await completion.</param>
        /// <returns><see langword="true"/> if handle is signaled; otherwise, <see langword="false"/> if timeout occurred.</returns>
        public static AwaitableWaitHandle WaitAsync(this WaitHandle handle, TimeSpan timeout)
        {
            if (handle.WaitOne(0))
                return AwaitableWaitHandle.Successful;
            else if (timeout == TimeSpan.Zero)
                return AwaitableWaitHandle.TimedOut;
            else
                return new AwaitableWaitHandle(handle, timeout);
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <returns>The task that will be completed .</returns>
        public static AwaitableWaitHandle WaitAsync(this WaitHandle handle) => WaitAsync(handle, InfiniteTimeSpan);
    }
}
