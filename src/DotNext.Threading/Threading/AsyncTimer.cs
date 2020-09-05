using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using FalseTask = Tasks.CompletedTask<bool, Generic.BooleanConst.False>;

    /// <summary>
    /// Represents asynchronous timer.
    /// </summary>
    /// <remarks>
    /// This timer provides guarantees than two executions of timer callback cannot be overlapped, i.e. executed twice or more concurrently at the same time.
    /// </remarks>
    public class AsyncTimer : Disposable, IAsyncDisposable
    {
        private sealed class TimerCompletionSource : TaskCompletionSource<bool>, IDisposable
        {
            private readonly CancellationTokenSource cancellation;

            internal TimerCompletionSource(CancellationToken token)
                => cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

            internal void RequestCancellation() => cancellation.Cancel();

            internal async void Execute(TimeSpan dueTime, TimeSpan period, ValueFunc<CancellationToken, Task<bool>> callback)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(dueTime, cancellation.Token).ConfigureAwait(false);
                    while (await callback.Invoke(cancellation.Token).ConfigureAwait(false))
                        await System.Threading.Tasks.Task.Delay(period, cancellation.Token).ConfigureAwait(false);
                    TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    TrySetResult(false);
                }
                catch (Exception e)
                {
                    TrySetException(e);
                }
                finally
                {
                    cancellation.Dispose();
                }
            }

            public void Dispose() => cancellation.Dispose();
        }

        private readonly ValueFunc<CancellationToken, Task<bool>> callback;
        [SuppressMessage("Usage", "CA2213", Justification = "It is disposed in Dispose method")]
        private volatile TimerCompletionSource? timerTask;
        private bool disposeRequested;

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

        /// <summary>
        /// Indicates that this timer is running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                var task = timerTask;
                return task != null && !task.Task.IsCompleted;
            }
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        /// <param name="dueTime">The amount of time to delay before the invoking the timer callback.</param>
        /// <param name="period">The time interval between invocations of the timer callback.</param>
        /// <param name="token">The token that can be used to stop execution of the timer.</param>
        /// <returns><see langword="true"/> if timer was in stopped state; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The timer has been disposed.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> is in canceled state.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Start(TimeSpan dueTime, TimeSpan period, CancellationToken token = default)
        {
            ThrowIfDisposed();
            token.ThrowIfCancellationRequested();
            if ((timerTask is null || timerTask.Task.IsCompleted) && !disposeRequested)
            {
                var source = new TimerCompletionSource(token);
                source.Execute(dueTime, period, callback);
                timerTask = source;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        /// <param name="period">The time interval between invocations of the timer callback.</param>
        /// <param name="token">The token that can be used to stop execution of the timer.</param>
        /// <returns><see langword="true"/> if timer was in stopped state; otherwise, <see langword="false"/>.</returns>
        public bool Start(TimeSpan period, CancellationToken token = default)
            => Start(period, period, token);

        /// <summary>
        /// Stops timer execution.
        /// </summary>
        /// <returns><see langword="true"/> if timer shutdown was initiated by the callback; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The timer has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> StopAsync()
        {
            if (IsDisposed)
                return GetDisposedTask<bool>();

            var result = timerTask;
            if (result is null || result.Task.IsCompleted)
                return FalseTask.Task;
            result.RequestCancellation();
            return result.Task;
        }

        /// <summary>
        /// Releases all resources associated with this timer.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            disposeRequested = true;
            if (disposing)
            {
                var timerTask = Interlocked.Exchange(ref this.timerTask, null);
                if (timerTask != null)
                {
                    TrySetDisposedException(timerTask);
                    timerTask.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void Dispose(Task parent, object state)
        {
            (state as IDisposable)?.Dispose();
            Dispose();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task DisposeAsyncImpl()
        {
            var result = Interlocked.Exchange(ref timerTask, null);
            if (result is null || disposeRequested)
            {
                Dispose();
                return Task.CompletedTask;
            }

            if (result.Task.IsCompleted)
            {
                result.Dispose();
                Dispose();
                return Task.CompletedTask;
            }

            disposeRequested = true;
            result.RequestCancellation();
            return result.Task.ContinueWith(Dispose, result, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        }

        /// <summary>
        /// Terminates timer gracefully.
        /// </summary>
        /// <returns>The task representing graceful shutdown.</returns>
        public ValueTask DisposeAsync() => new ValueTask(IsDisposed ? Task.CompletedTask : DisposeAsyncImpl());
    }
}
