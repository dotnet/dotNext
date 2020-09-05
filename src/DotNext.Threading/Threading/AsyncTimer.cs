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
            private const byte BeginInitState = 0;
            private const byte EndInitState = 1;
            private const byte BeginLoopState = 2;
            private const byte EndLoopState = 3;
            private const byte BeginDelayState = 4;
            private const byte EndDelayState = 5;
            private readonly CancellationTokenSource cancellation;
            private readonly TimeSpan dueTime, period;
            private readonly ValueFunc<CancellationToken, Task<bool>> callback;

            // state management
            private readonly Action continuation;
            private byte state;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter voidAwaiter;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter boolAwaiter;

            internal TimerCompletionSource(TimeSpan dueTime, TimeSpan period, ValueFunc<CancellationToken, Task<bool>> callback, CancellationToken token)
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                this.dueTime = dueTime;
                this.period = period;
                this.callback = callback;
                continuation = Execute;
            }

            internal void RequestCancellation() => cancellation.Cancel();

            private void Execute()
            {
                try
                {
                    switch (state)
                    {
                        case BeginInitState:
                            voidAwaiter = System.Threading.Tasks.Task.Delay(dueTime, cancellation.Token).ConfigureAwait(false).GetAwaiter();
                            if (voidAwaiter.IsCompleted)
                                goto case EndInitState;
                            state = EndInitState;
                            voidAwaiter.OnCompleted(continuation);
                            break;
                        case EndInitState:
                            voidAwaiter.GetResult();
                            voidAwaiter = default;
                            goto case BeginLoopState;
                        case BeginLoopState:
                            boolAwaiter = callback.Invoke(cancellation.Token).ConfigureAwait(false).GetAwaiter();
                            if (boolAwaiter.IsCompleted)
                                goto case EndLoopState;
                            state = EndLoopState;
                            boolAwaiter.OnCompleted(continuation);
                            break;
                        case EndLoopState:
                            var success = boolAwaiter.GetResult();
                            boolAwaiter = default;
                            if (success)
                                goto case BeginDelayState;
                            TrySetResult(true);
                            break;
                        case BeginDelayState:
                            voidAwaiter = System.Threading.Tasks.Task.Delay(period, cancellation.Token).ConfigureAwait(false).GetAwaiter();
                            if (voidAwaiter.IsCompleted)
                                goto case EndDelayState;
                            state = EndDelayState;
                            voidAwaiter.OnCompleted(continuation);
                            break;
                        case EndDelayState:
                            voidAwaiter.GetResult();
                            voidAwaiter = default;
                            goto case BeginLoopState;
                    }
                }
                catch (OperationCanceledException)
                {
                    TrySetResult(false);
                }
                catch (Exception e)
                {
                    TrySetException(e);
                }

                if (Task.IsCompleted)
                    cancellation.Dispose();
            }

            internal void Start()
            {
                ThreadPool.QueueUserWorkItem(Start, this, false);

                static void Start(TimerCompletionSource source) => source.Execute();
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    cancellation.Dispose();
                    voidAwaiter = default;
                    boolAwaiter = default;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~TimerCompletionSource() => Dispose(false);
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
                var source = new TimerCompletionSource(dueTime, period, callback, token);
                source.Start();
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
            if (result is null)
            {
            }
            else if (result.Task.IsCompleted)
            {
                result.Dispose();
            }
            else
            {
                disposeRequested = true;
                result.RequestCancellation();
                return result.Task.ContinueWith(Dispose, result, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
            }

            Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Terminates timer gracefully.
        /// </summary>
        /// <returns>The task representing graceful shutdown.</returns>
        public ValueTask DisposeAsync() => new ValueTask(IsDisposed ? Task.CompletedTask : DisposeAsyncImpl());
    }
}
