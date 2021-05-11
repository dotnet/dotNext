using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Tasks;

    /// <summary>
    /// Represents a synchronization point at which two async flows can cooperate and swap elements
    /// within pairs.
    /// </summary>
    /// <remarks>
    /// This type is useful to organize pipeline between consumer and producer.
    /// </remarks>
    /// <typeparam name="T">The type of objects that may be exchanged.</typeparam>
    public class AsyncExchanger<T> : Disposable, IAsyncDisposable
    {
        private sealed class ExchangePoint : CancelableCompletionSource<T>
        {
            private readonly T producerResult;

            internal ExchangePoint(T result, TimeSpan timeout, CancellationToken token)
                : base(TaskCreationOptions.RunContinuationsAsynchronously, timeout, token)
                => producerResult = result;

            internal bool TryExchange(ref T value)
            {
                if (TrySetResult(value))
                {
                    value = producerResult;
                    return true;
                }

                return false;
            }
        }

        [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly but cannot be recognized by .NET Analyzer")]
        private volatile ExchangePoint? point;
        private bool disposeRequested;
        private volatile ExchangeTerminatedException? termination;

        /// <summary>
        /// Waits for another flow to arrive at this exchange point,
        /// then transfers the given object to it, receiving its object as return value.
        /// </summary>
        /// <param name="value">The object to exchange.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The object provided by another async flow.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled or timed out.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<T> ExchangeAsync(T value, TimeSpan timeout, CancellationToken token = default)
        {
            ValueTask<T> result;

            if (IsDisposed)
            {
                result = new(GetDisposedTask<T>());
            }
            else if (disposeRequested)
            {
                Dispose();
                result = new(GetDisposedTask<T>());
            }
            else if (termination is not null)
            {
#if NETSTANDARD2_1
                result = new(Task.FromException<T>(termination));
#else
                result = ValueTask.FromException<T>(termination);
#endif
            }
            else if (point is null)
            {
                result = new(point = new ExchangePoint(value, timeout, token));
            }
            else if (point.TryExchange(ref value))
            {
                point.Dispose();
                point = null;
                result = new(value);
            }
            else
            {
                point.Dispose();
                result = new(point = new ExchangePoint(value, timeout, token));
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
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
        public ValueTask<T> ExchangeAsync(T value, CancellationToken token = default)
            => ExchangeAsync(value, InfiniteTimeSpan, token);

        /// <summary>
        /// Attempts to transfer the object to another flow synchronously.
        /// </summary>
        /// <remarks>
        /// <paramref name="value"/> remains unchanged if return value is <see langword="false"/>.
        /// </remarks>
        /// <param name="value">The object to exchange.</param>
        /// <returns><see langword="true"/> if another flow is ready for exchange; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="ExchangeTerminatedException">The exhange has been terminated.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryExchange(ref T value)
        {
            ThrowIfDisposed();

            bool result;
            if (disposeRequested)
            {
                Dispose();
                result = false;
            }
            else if (termination is not null)
            {
                throw termination;
            }
            else if (point is null)
            {
                result = false;
            }
            else
            {
                result = point.TryExchange(ref value);
                point.Dispose();
                point = null;
            }

            return result;
        }

        /// <summary>
        /// Informs another participant that no more data will be exchanged with it.
        /// </summary>
        /// <param name="exception">The optional exception indicating termination reason.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="InvalidOperationException">The exchange is already terminated.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate(Exception? exception = null)
        {
            ThrowIfDisposed();
            if (termination is not null)
                throw new InvalidOperationException();

            ExchangeTerminatedException tmp;
            termination = tmp = new ExchangeTerminatedException(exception);
            if (point?.TrySetException(tmp) ?? false)
            {
                point.Dispose();
                point = null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this exchange has been terminated.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public bool IsTerminated
        {
            get
            {
                ThrowIfDisposed();
                return termination is not null;
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            disposeRequested = true;
            if (disposing)
            {
                var point = Interlocked.Exchange(ref this.point, null);
                if (point is not null && TrySetDisposedException(point))
                    point.Dispose();
                termination = null;
            }

            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task DisposeAsyncImpl()
        {
            disposeRequested = true;

            if (point is null || point.Task.IsCompleted)
            {
                Dispose();
                return Task.CompletedTask;
            }

            return point.Task.ContinueWith(SuppressFaultOrCancellation, point, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

            static void SuppressFaultOrCancellation(Task<T> task, object? state)
                => (state as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Provides graceful shutdown of this instance.
        /// </summary>
        /// <returns>The task representing state of asynchronous graceful shutdown.</returns>
        public ValueTask DisposeAsync()
            => new(IsDisposed ? Task.CompletedTask : DisposeAsyncImpl());
    }
}