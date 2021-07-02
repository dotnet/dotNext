#if !NETSTANDARD2_1
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using static System.Threading.Timeout;
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
    /// for the task itself (excluding continuations). See <see cref="Reset(TimeSpan, CancellationToken)"/>
    /// for more information.
    /// The instance of this type typically used in combination with object pool pattern because
    /// the instance can be reused for multiple tasks. The first usage pattern allows to reuse the instance
    /// for the multiple completions if the task was not canceled or timed out:
    /// 1. Retrieve instance of this type from the pool and call <see cref="Reset(TimeSpan, CancellationToken)"/>.
    /// 2. Complete the task with <see cref="TrySetResult(T)"/> or <see cref="TrySetException(Exception)"/>.
    /// 3. If completion method returns <see langword="true"/> then return the instance back to the pool.
    /// If completion method returns <see langword="false"/> then the task was canceled or timed out. In this
    /// case you cannot reuse the instance in simple way.
    /// To reuse instance in case of cancellation, you need to override <see cref="BeforeCompleted(Result{T})"/>
    /// and <see cref="ValueTaskCompletionSourceBase.AfterConsumed"/> methods. The first one to remove the source
    /// from the list of active sources. The second one to return the instance back to the pool.
    /// </remarks>
    /// <typeparam name="T">>The type the task result.</typeparam>
    public class ValueTaskCompletionSource<T> : ValueTaskCompletionSourceBase, IValueTaskSource<T>
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
        /// <param name="completionToken">The completion token previously obtained from <see cref="Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="value">The value to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetResult(short completionToken, T value)
            => TrySetResult(completionToken, &Result.FromValue, value);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetException(Exception e)
            => TrySetResult(&Result.FromException<T>, e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetException(short completionToken, Exception e)
            => TrySetResult(completionToken, &Result.FromException<T>, e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetCanceled(CancellationToken token)
            => TrySetResult(&Result.FromCanceled<T>, token);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public unsafe bool TrySetCanceled(short completionToken, CancellationToken token)
            => TrySetResult(completionToken, &Result.FromCanceled<T>, token);

        private protected override void CompleteAsTimedOut()
            => SetResult(OnTimeout());

        private protected override void CompleteAsCanceled(CancellationToken token)
            => SetResult(OnCanceled(token));

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
                lock (syncRoot)
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
                lock (syncRoot)
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
            Recycle();
            try
            {
                // run handler before actual completion to avoid concurrency with AfterConsumed event
                BeforeCompleted(result);
            }
            finally
            {
                this.result = result;
                completed = true;
                InvokeContinuation();
            }
        }

        [CallerMustBeSynchronized]
        private protected override void ResetCore()
        {
            base.ResetCore();
            result = default;
        }

        [CallerMustBeSynchronized]
        private ValueTask<T> CreateTaskCore(TimeSpan timeout, CancellationToken token)
        {
            if (timeout == TimeSpan.Zero)
            {
                SetResult(OnTimeout());
                goto exit;
            }

            if (token.IsCancellationRequested)
            {
                SetResult(OnCanceled(token));
                goto exit;
            }

            Configure(timeout, token);

        exit:
            return new(this, version);
        }

        /// <summary>
        /// Resets the state of the underlying task and return a fresh incompleted task.
        /// </summary>
        /// <remarks>
        /// The returned task can be completed in a three ways: through cancellation token, timeout
        /// or by calling <see cref="TrySetException(Exception)"/> or <see cref="TrySetResult(T)"/>.
        /// If <paramref name="timeout"/> is <see cref="InfiniteTimeSpan"/> then this source doesn't
        /// track the timeout. If <paramref name="token"/> is not cancelable then this source
        /// doesn't track the cancellation. If both conditions are met then this source doesn't allocate
        /// additional memory on the heap. Otherwise, the allocation is very minimal and needed
        /// for cancellation registrations.
        /// This method can be called safely in the following circumstances: after construction of a new
        /// instance of this class or after (or during) the call of <see cref="ValueTaskCompletionSourceBase.AfterConsumed"/> method.
        /// </remarks>
        /// <param name="completionToken">The version of the produced task that can be used later to complete the task without conflicts.</param>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        public ValueTask<T> Reset(out short completionToken, TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!completed)
                throw new InvalidOperationException();

            lock (syncRoot)
            {
                ResetCore();
                completionToken = version;
                return CreateTaskCore(timeout, token);
            }
        }

        /// <summary>
        /// Resets the state of the underlying task and return a fresh incompleted task.
        /// </summary>
        /// <remarks>
        /// The returned task can be completed in a three ways: through cancellation token, timeout
        /// or by calling <see cref="TrySetException(Exception)"/> or <see cref="TrySetResult(T)"/>.
        /// If <paramref name="timeout"/> is <see cref="InfiniteTimeSpan"/> then this source doesn't
        /// track the timeout. If <paramref name="token"/> is not cancelable then this source
        /// doesn't track cancellation. If both conditions are met then this source doesn't allocate
        /// additional memory on the heap. Otherwise, the allocation is very minimal and needed
        /// for cancellation registrations.
        /// This method can be called safely in the following circumstances: after construction of a new
        /// instance of this class or after (or during) the call of <see cref="ValueTaskCompletionSourceBase.AfterConsumed"/> method.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        public ValueTask<T> Reset(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!completed)
                throw new InvalidOperationException();

            ValueTask<T> result;
            lock (syncRoot)
            {
                ResetCore();
                result = CreateTaskCore(timeout, token);
            }

            return result;
        }

        /// <summary>
        /// Creates a fresh task linked with this source.
        /// </summary>
        /// <remarks>
        /// This method must be called after <see cref="ValueTaskCompletionSourceBase.Reset()"/>.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        public ValueTask<T> CreateTask(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            ValueTask<T> result;

            if (completed)
            {
                result = new(this, version);
            }
            else
            {
                lock (syncRoot)
                {
                    result = completed ? new(this, version) : CreateTaskCore(timeout, token);
                }
            }

            return result;
        }

        /// <summary>
        /// Invokes when the task is almost completed.
        /// </summary>
        /// <remarks>
        /// This method is called before <see cref="ValueTaskCompletionSourceBase.AfterConsumed"/>.
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

            if (IsDerived)
                QueueAfterConsumed();

            return result.Value;
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
        {
            var capturedContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == 0 ? null : CaptureContext();
            OnCompleted(capturedContext, continuation, state, token, (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0);
        }
    }
}
#endif