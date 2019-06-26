using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Tasks;
    using True = Generic.BooleanConst.True;
    using False = Generic.BooleanConst.False;

    /// <summary>
    /// Allows to turn <see cref="WaitHandle"/> and <see cref="CancellationToken"/> into task.
    /// </summary>
    public static class AsyncBridge
    {
        private sealed class WaitHandleCompletionSource : TaskCompletionSource<bool>, IDisposable
        {
            private readonly RegisteredWaitHandle handle;

            public WaitHandleCompletionSource(WaitHandle handle, TimeSpan timeout)
                : base(TaskCreationOptions.None)
            {
                this.handle = ThreadPool.RegisterWaitForSingleObject(handle, OnTimeout, null, timeout, true);
            }

            private void OnTimeout(object state, bool timedOut)
            {
                handle.Unregister(null);
                TrySetResult(!timedOut);
            }

            void IDisposable.Dispose() => handle.Unregister(null);
        }

        [SuppressMessage("Design", "CA1068", Justification = "This is token conversion method so position of token parameter is by design")]
        private static async Task TokenToTask(CancellationToken token, bool completedAsCanceled)
        {
            var source = new CancelableTaskCompletionSource<bool>(ref token, TaskCreationOptions.None);
            try
            {
                await source.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (completedAsCanceled)
                    throw;
            }
            finally
            {
                source.Dispose();
            }
        }

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
            if (token.IsCancellationRequested)
                return completeAsCanceled ? Task.FromCanceled(token) : Task.CompletedTask;
            return TokenToTask(token, completeAsCanceled);
        }

        private static async Task<bool> HandleToTask(WaitHandle handle, TimeSpan timeout)
        {
            using (var source = new WaitHandleCompletionSource(handle, timeout))
                return await source.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <param name="timeout">The timeout used to await completion.</param>
        /// <returns><see langword="true"/> if handle is signaled; otherwise, <see langword="false"/> if timeout occurred.</returns>
        public static Task<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout)
        {
            var completed = handle.WaitOne(0);
            if (completed)
                return CompletedTask<bool, True>.Task;
            else if (timeout == TimeSpan.Zero)
                return CompletedTask<bool, False>.Task;
            else
                return HandleToTask(handle, timeout);
        }

        /// <summary>
        /// Obtains a task that can be used to await handle completion.
        /// </summary>
        /// <param name="handle">The handle to await.</param>
        /// <returns>The task that will be completed .</returns>
        public static Task WaitAsync(this WaitHandle handle) => WaitAsync(handle, InfiniteTimeSpan);
    }
}
