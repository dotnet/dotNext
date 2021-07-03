#if !NETSTANDARD2_1
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks
{
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;
    using NullExceptionConstant = Generic.DefaultConst<Exception?>;

    /// <summary>
    /// Represents the producer side of <see cref="ValueTask"/>.
    /// </summary>
    /// <remarks>
    /// See description of <see cref="ValueTaskCompletionSource{T}"/> for more information
    /// about behavior of the completion source.
    /// </remarks>
    /// <seealso cref="ValueTaskCompletionSource{T}"/>
    public class ValueTaskCompletionSource : ManualResetCompletionSource<ValueTask>, IValueTaskSource
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
            result = ExceptionDispatchInfo.Capture(new InvalidOperationException(ExceptionMessages.CompletionSourceInitialState));
            completed = true;
        }

        private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource);

        private protected sealed override ValueTask Task => new(this, version);

        [CallerMustBeSynchronized]
        private void SetResult(Exception? result)
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            StopTrackingCancellation();
            try
            {
                // run handler before actual completion to avoid concurrency with AfterConsumed event
                BeforeCompleted(result);
            }
            catch (Exception e)
            {
                this.result = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                this.result = result is null ? null : ExceptionDispatchInfo.Capture(result);
                completed = true;
                InvokeContinuation();
            }
        }

        private protected sealed override void CompleteAsTimedOut()
            => SetResult(OnTimeout());

        private protected sealed override void CompleteAsCanceled(CancellationToken token)
            => SetResult(OnCanceled(token));

        [CallerMustBeSynchronized]
        private protected sealed override void ResetCore()
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            base.ResetCore();
            result = null;
        }

        /// <summary>
        /// Invokes when the task is almost completed.
        /// </summary>
        /// <remarks>
        /// This method is called before <see cref="ManualResetCompletionSource.AfterConsumed"/>.
        /// </remarks>
        /// <param name="e">The exception associated with the task or <see langword="null"/> if completed successfully.</param>
        protected virtual void BeforeCompleted(Exception? e)
        {
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

        private unsafe bool TrySetResult<TFactory>(TFactory factory)
            where TFactory : notnull, ISupplier<Exception?>
        {
            bool result;
            if (completed)
            {
                result = false;
            }
            else
            {
                lock (SyncRoot)
                {
                    if (completed)
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

        private unsafe bool TrySetResult<TFactory>(short completionToken, TFactory factory)
            where TFactory : notnull, ISupplier<Exception?>
        {
            bool result;
            if (completed)
            {
                result = false;
            }
            else
            {
                lock (SyncRoot)
                {
                    if (completed || completionToken != version)
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
        public sealed override unsafe bool TrySetCanceled(CancellationToken token)
            => TrySetResult<OperationCanceledExceptionFactory>(token);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetCanceled(short completionToken, CancellationToken token)
            => TrySetResult<OperationCanceledExceptionFactory>(completionToken, token);

         /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public sealed override unsafe bool TrySetException(Exception e)
            => TrySetResult<ValueSupplier<Exception>>(e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetException(short completionToken, Exception e)
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
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public bool TrySetResult(short completionToken)
            => TrySetResult(completionToken, NullSupplier);

        /// <inheritdoc />
        void IValueTaskSource.GetResult(short token)
        {
            if (!completed || token != version)
                throw new InvalidOperationException();

            // ensure that instance field access before returning to the pool to avoid
            // concurrency with Reset()
            var resultCopy = result;

            if (IsDerived)
                QueueAfterConsumed();

            resultCopy?.Throw();
        }

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            if (!completed)
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
#endif