using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks
{
    using NullExceptionConstant = Generic.DefaultConst<Exception?>;

    /// <summary>
    /// Represents the producer side of <see cref="ValueTask"/>.
    /// </summary>
    /// <remarks>
    /// See description of <see cref="ValueTaskCompletionSource{T}"/> for more information
    /// about behavior of the completion source.
    /// </remarks>
    /// <seealso cref="ValueTaskCompletionSource{T}"/>
    public class ValueTaskCompletionSource : ManualResetCompletionSource, IValueTaskSource, ISupplier<TimeSpan, CancellationToken, ValueTask>
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct OperationCanceledExceptionFactory : ISupplier<OperationCanceledException>
        {
            private readonly CancellationToken token;

            internal OperationCanceledExceptionFactory(CancellationToken token) => this.token = token;

            OperationCanceledException ISupplier<OperationCanceledException>.Invoke()
                => new(token);

            public static implicit operator OperationCanceledExceptionFactory(CancellationToken token)
                => new(token);
        }

        private static readonly NullExceptionConstant NullSupplier = new();

        // null - success, not null - error
        private ExceptionDispatchInfo? result;

        /// <summary>
        /// Initializes a new completion source.
        /// </summary>
        /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
        public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
            : base(runContinuationsAsynchronously)
        {
        }

        private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource);

        private void SetResult(Exception? result)
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            StopTrackingCancellation();
            this.result = result is null ? null : ExceptionDispatchInfo.Capture(result);
            IsCompleted = true;
            InvokeContinuation();
        }

        private protected sealed override void CompleteAsTimedOut()
            => SetResult(OnTimeout());

        private protected sealed override void CompleteAsCanceled(CancellationToken token)
            => SetResult(OnCanceled(token));

        private protected sealed override void ResetCore()
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            base.ResetCore();
            result = null;
        }

        /// <summary>
        /// Called automatically when timeout detected.
        /// </summary>
        /// <remarks>
        /// By default, this method returns <see cref="TimeoutException"/> as the task result.
        /// </remarks>
        /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
        protected virtual Exception? OnTimeout() => new TimeoutException();

        /// <summary>
        /// Called automatically when cancellation detected.
        /// </summary>
        /// <remarks>
        /// By default, this method returns <see cref="OperationCanceledException"/> as the task result.
        /// </remarks>
        /// <param name="token">The token representing cancellation reason.</param>
        /// <returns>The exception representing task result; or <see langword="null"/> to complete successfully.</returns>
        protected virtual Exception? OnCanceled(CancellationToken token) => new OperationCanceledException(token);

        private bool TrySetResult<TFactory>(TFactory factory)
            where TFactory : notnull, ISupplier<Exception?>
        {
            bool result;
            if (IsCompleted)
            {
                result = false;
            }
            else
            {
                lock (SyncRoot)
                {
                    if (IsCompleted)
                    {
                        result = false;
                    }
                    else
                    {
                        SetResult(factory.Invoke());
                        result = true;
                    }
                }
            }

            return result;
        }

        private bool TrySetResult<TFactory>(short completionToken, TFactory factory)
            where TFactory : notnull, ISupplier<Exception?>
        {
            bool result;
            if (IsCompleted)
            {
                result = false;
            }
            else
            {
                lock (SyncRoot)
                {
                    if (IsCompleted || completionToken != version)
                    {
                        result = false;
                    }
                    else
                    {
                        SetResult(factory.Invoke());
                        result = true;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public sealed override bool TrySetCanceled(CancellationToken token)
            => TrySetResult<OperationCanceledExceptionFactory>(token);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public bool TrySetCanceled(short completionToken, CancellationToken token)
            => TrySetResult<OperationCanceledExceptionFactory>(completionToken, token);

         /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public sealed override bool TrySetException(Exception e)
            => TrySetResult<ValueSupplier<Exception>>(e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public bool TrySetException(short completionToken, Exception e)
            => TrySetResult<ValueSupplier<Exception>>(completionToken, e);

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public bool TrySetResult()
            => TrySetResult(NullSupplier);

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="CreateTask(TimeSpan, CancellationToken)"/> method.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public bool TrySetResult(short completionToken)
            => TrySetResult(completionToken, NullSupplier);

        /// <summary>
        /// Creates a fresh task linked with this source.
        /// </summary>
        /// <remarks>
        /// This method must be called after <see cref="ManualResetCompletionSource.Reset()"/>.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than zero but not equals to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</exception>
        public ValueTask CreateTask(TimeSpan timeout, CancellationToken token)
        {
            PrepareTask(timeout, token);
            return new(this, version);
        }

        /// <inheritdoc />
        ValueTask ISupplier<TimeSpan, CancellationToken, ValueTask>.Invoke(TimeSpan timeout, CancellationToken token)
            => CreateTask(timeout, token);

        /// <inheritdoc />
        void IValueTaskSource.GetResult(short token)
        {
            if (!IsCompleted || token != version)
                throw new InvalidOperationException();

            // ensure that instance field access before returning to the pool to avoid
            // concurrency with Reset()
            var resultCopy = result;
            Thread.MemoryBarrier();

            if (IsDerived)
                QueueAfterConsumed();

            resultCopy?.Throw();
        }

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            if (!IsCompleted)
                return ValueTaskSourceStatus.Pending;

            if (result is null)
                return ValueTaskSourceStatus.Succeeded;

            return result.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted;
        }

        /// <inheritdoc />
        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => OnCompleted(continuation, state, token, flags);
    }
}