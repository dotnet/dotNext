#if !NETSTANDARD2_1
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks
{
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

    /// <summary>
    /// Represents the producer side of <see cref="ValueTask{T}"/>.
    /// </summary>
    /// <remarks>
    /// In constrast to <see cref="TaskCompletionSource{T}"/>, this
    /// source is resettable.
    /// From performance point of view, the type offers minimal or zero memory allocation
    /// for the task itself (excluding continuations). See <see cref="ManualResetCompletionSource{T}.Reset(TimeSpan, CancellationToken)"/>
    /// for more information.
    /// The instance of this type typically used in combination with object pool pattern because
    /// the instance can be reused for multiple tasks. The first usage pattern allows to reuse the instance
    /// for the multiple completions if the task was not canceled or timed out:
    /// 1. Retrieve instance of this type from the pool and call <see cref="ManualResetCompletionSource{T}.Reset(TimeSpan, CancellationToken)"/>.
    /// 2. Complete the task with <see cref="TrySetResult(T)"/> or <see cref="TrySetException(Exception)"/>.
    /// 3. If completion method returns <see langword="true"/> then return the instance back to the pool.
    /// If completion method returns <see langword="false"/> then the task was canceled or timed out. In this
    /// case you cannot reuse the instance in simple way.
    /// To reuse instance in case of cancellation, you need to override <see cref="BeforeCompleted(Result{T})"/>
    /// and <see cref="ManualResetCompletionSource.AfterConsumed"/> methods. The first one to remove the source
    /// from the list of active sources. The second one to return the instance back to the pool.
    /// </remarks>
    /// <typeparam name="T">>The type the task result.</typeparam>
    /// <seealso cref="ValueTaskCompletionSource"/>
    public class ValueTaskCompletionSource<T> : ManualResetCompletionSource<ValueTask<T>>, IValueTaskSource<T>
    {
        private Result<T> result;

        /// <summary>
        /// Initializes a new completion source.
        /// </summary>
        /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
        public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
            : base(runContinuationsAsynchronously)
        {
            result = new(new InvalidOperationException(ExceptionMessages.CompletionSourceInitialState));
            completed = true;
        }

        private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource<T>);

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <param name="value">The value to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetResult(T value)
            => TrySetResult(&Result.FromValue, value);

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="value">The value to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetResult(short completionToken, T value)
            => TrySetResult(completionToken, &Result.FromValue, value);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public sealed override unsafe bool TrySetException(Exception e)
            => TrySetResult(&Result.FromException<T>, e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetException(short completionToken, Exception e)
            => TrySetResult(completionToken, &Result.FromException<T>, e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public sealed override unsafe bool TrySetCanceled(CancellationToken token)
            => TrySetResult(&Result.FromCanceled<T>, token);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="ManualResetCompletionSource{T}.Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetCanceled(short completionToken, CancellationToken token)
            => TrySetResult(completionToken, &Result.FromCanceled<T>, token);

        private protected sealed override void CompleteAsTimedOut()
            => SetResult(OnTimeout());

        private protected sealed override void CompleteAsCanceled(CancellationToken token)
            => SetResult(OnCanceled(token));

        private protected sealed override ValueTask<T> Task => new(this, version);

        private unsafe bool TrySetResult<TArg>(delegate*<TArg, Result<T>> func, TArg arg)
        {
            Debug.Assert(func != null);

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
                        SetResult(func(arg));
                        result = true;
                    }
                }
            }

            return result;
        }

        private unsafe bool TrySetResult<TArg>(short completionToken, delegate*<TArg, Result<T>> func, TArg arg)
        {
            Debug.Assert(func != null);

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
                        SetResult(func(arg));
                        result = true;
                    }
                }
            }

            return result;
        }

        [CallerMustBeSynchronized]
        private void SetResult(Result<T> result)
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
                this.result = new(e);
            }
            finally
            {
                this.result = result;
                completed = true;
                InvokeContinuation();
            }
        }

        [CallerMustBeSynchronized]
        private protected sealed override void ResetCore()
        {
            Debug.Assert(Monitor.IsEntered(SyncRoot));

            base.ResetCore();
            result = default;
        }

        /// <summary>
        /// Invokes when the task is almost completed.
        /// </summary>
        /// <remarks>
        /// This method is called before <see cref="ManualResetCompletionSource.AfterConsumed"/>.
        /// </remarks>
        /// <param name="result">The result of the task.</param>
        protected virtual void BeforeCompleted(Result<T> result)
        {
        }

        /// <summary>
        /// Called automatically when timeout detected.
        /// </summary>
        /// <remarks>
        /// By default, this method assigns <see cref="TimeoutException"/> as the task result.
        /// </remarks>
        /// <returns>The result to be assigned to the task.</returns>
        protected virtual Result<T> OnTimeout() => new(new TimeoutException());

        /// <summary>
        /// Called automatically when cancellation detected.
        /// </summary>
        /// <remarks>
        /// By default, this method assigns <see cref="OperationCanceledException"/> as the task result.
        /// </remarks>
        /// <param name="token">The token representing cancellation reason.</param>
        /// <returns>The result to be assigned to the task.</returns>
        protected virtual Result<T> OnCanceled(CancellationToken token) => new(new OperationCanceledException(token));

        /// <inheritdoc />
        T IValueTaskSource<T>.GetResult(short token)
        {
            if (!completed || token != version)
                throw new InvalidOperationException();

            // ensure that instance field access before returning to the pool to avoid
            // concurrency with Reset()
            var resultCopy = result;

            if (IsDerived)
                QueueAfterConsumed();

            return resultCopy.Value;
        }

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
        {
            if (!completed)
                return ValueTaskSourceStatus.Pending;

            var error = result.Error;
            if (error is null)
                return ValueTaskSourceStatus.Succeeded;

            return error is OperationCanceledException ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted;
        }

        /// <inheritdoc />
        void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => OnCompleted(continuation, state, token, flags);
    }
}
#endif