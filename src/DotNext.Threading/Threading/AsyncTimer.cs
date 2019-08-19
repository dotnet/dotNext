using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous timer.
    /// </summary>
    /// <remarks>
    /// This timer provides guarantees than two executions of timer callback cannot be overlapped, i.e. executed twice or more concurrently at the same time.
    /// </remarks>
    public class AsyncTimer : Disposable
    {
        private volatile RegisteredWaitHandle timerHandle;
        private readonly ValueFunc<CancellationToken, Task<bool>> callback;
        private AtomicBoolean processingState;

        /// <summary>
        /// Initializes a new timer.
        /// </summary>
        /// <param name="callback">The timer callback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="callback"/> doesn't refer to any method.</exception>
        public AsyncTimer(ValueFunc<CancellationToken, Task<bool>> callback)
        {
            if (callback.IsEmpty)
                throw new ArgumentException(ExceptionMessages.EmptyValueDelegate, nameof(callback));
            this.callback = callback;
        }

        /// <summary>
        /// Initializes a new timer.
        /// </summary>
        /// <param name="callback">The timer callback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public AsyncTimer(Func<CancellationToken, Task<bool>> callback)
            : this(new ValueFunc<CancellationToken, Task<bool>>(callback))
        {
        }

        private async void OnTimer(object state, bool timedOut)
        {
            if (IsDisposed)
            { }
            else if (!timedOut)
                Stop();
            else if (state is CancellationToken token && processingState.FalseToTrue())
                try
                {
                    if (!await callback.Invoke(token).ConfigureAwait(false))
                        Stop();
                }
                finally
                {
                    processingState.Value = false;
                }

        }

        /// <summary>
        /// Indicates that this timer is running.
        /// </summary>
        public bool IsRunning => timerHandle != null;

        /// <summary>
        /// Starts the timer.
        /// </summary>
        /// <param name="period">The time interval between invocations of the timer callback.</param>
        /// <param name="token">The token that can be used to stop execution of the timer.</param>
        /// <returns><see langword="true"/> if timer was in stopped state; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Start(TimeSpan period, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (timerHandle != null) return false;
            processingState.Value = false;
            timerHandle = ThreadPool.RegisterWaitForSingleObject(token.WaitHandle, OnTimer, token, period, false);
            return true;

        }

        /// <summary>
        /// Stops timer execution.
        /// </summary>
        /// <param name="stopHandle">The handle to be signaled when timer stops gracefully.</param>
        /// <returns><see langword="true"/> if timer was in started state; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Stop(WaitHandle stopHandle = null)
        {
            if (timerHandle is null)
                return false;
            var result = timerHandle.Unregister(stopHandle);
            timerHandle = null;
            return result;
        }

        /// <summary>
        /// Stops timer execution.
        /// </summary>
        /// <param name="timeout">The timeout used to wait for timer graceful shutdown.</param>
        /// <returns><see langword="true"/> if timer was in started state; <see langword="false"/> if timeout reached.</returns>
        public async Task<bool> StopAsync(TimeSpan timeout)
        {
            using (var notifier = new ManualResetEvent(false))
                return Stop(notifier) && await notifier.WaitAsync(timeout);
        }

        /// <summary>
        /// Stops timer execution.
        /// </summary>
        /// <returns><see langword="true"/> if timer was in started state; otherwise, <see langword="false"/>.</returns>
        public Task<bool> StopAsync() => StopAsync(InfiniteTimeSpan);

        /// <summary>
        /// Releases all resources associated with this timer.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerHandle?.Unregister(null);
                timerHandle = null;
            }
            base.Dispose(disposing);
        }
    }
}
