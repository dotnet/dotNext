using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using static Tasks.Continuation;

    /// <summary>
    /// A synchronization point at which two async flows can cooperate and swap elements
    /// within pairs.
    /// </summary>
    /// <remarks>
    /// This type is useful to organize pipeline between consumer and producer.
    /// </remarks>
    /// <typeparam name="T">The type of objects that may be exchanged.</typeparam>
    public class AsyncExchanger<T> : Disposable, IAsyncDisposable
    {
        private sealed class ExchangePoint : TaskCompletionSource<T>
        {
            private readonly T producerResult;

            internal ExchangePoint(T result, bool runContinuationsAsynchronously)
                : base(runContinuationsAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None)
                => producerResult = result;

            internal T Exchange(T value)
            {
                TrySetResult(value);
                return producerResult;
            }
        }

        private readonly bool runContinuationsAsynchronously;
        private ExchangePoint? point;
        private bool disposeRequested;

        /// <summary>
        /// Initializes a new asynchronous exchanger.
        /// </summary>
        /// <param name="runContinuationsAsynchronously"><see langword="true"/> to run continuations asynchronously; otherwise, <see langword="false"/>.</param>
        public AsyncExchanger(bool runContinuationsAsynchronously = true)
            => this.runContinuationsAsynchronously = runContinuationsAsynchronously;

        /// <summary>
        /// Waits for another flow to arrive at this exchange point,
        /// then transfers the given object to it, receiving its object as return value.
        /// </summary>
        /// <param name="value">The object to exchange.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The object provided by another async flow.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="TimeoutException">The timeout has occurred.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<T> ExchangeAsync(T value, TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValueTask<T> result;

            if (disposeRequested)
            {
                Dispose();
                result = new ValueTask<T>(Task.FromException<T>(new ObjectDisposedException(GetType().Name)));
            }
            else if (point is null)
            {
                point = new ExchangePoint(value, runContinuationsAsynchronously);
                result = new ValueTask<T>(point.Task.ContinueWithTimeout(timeout, token));
            }
            else
            {
                result = new ValueTask<T>(point.Exchange(value));
                point = null;
            }

            return result;
        }

        /// <summary>
        /// Waits for another flow to arrive at this exchange point,
        /// then transfers the given object to it, receiving its object as return value.
        /// </summary>
        /// <param name="value">The object to exchange.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The object provided by another async flow.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<T> ExchangeAsync(T value, CancellationToken token = default)
            => ExchangeAsync(value, InfiniteTimeSpan, token);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            disposeRequested = true;
            if (disposing)
            {
                Interlocked.Exchange(ref point, null)?.TrySetException(new ObjectDisposedException(GetType().Name));
            }

            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task DisposeAsyncImpl()
        {
            disposeRequested = true;

            if (point is null)
            {
                Dispose();
                return Task.CompletedTask;
            }

            return point.Task.ContinueWith(SuppressFaultOrCancellation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

            static void SuppressFaultOrCancellation(Task<T> task)
            {
            }
        }

        /// <summary>
        /// Provides graceful shutdown of this instance.
        /// </summary>
        /// <returns>The task representing state of asynchronous graceful shutdown.</returns>
        public ValueTask DisposeAsync()
            => new ValueTask(IsDisposed ? Task.CompletedTask : DisposeAsyncImpl());
    }
}