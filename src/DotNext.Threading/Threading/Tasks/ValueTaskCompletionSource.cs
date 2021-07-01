#if !NETSTANDARD2_1
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using static System.Threading.Timeout;

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
    /// and <see cref="AfterConsumed"/> methods. The first one to remove the source
    /// from the list of active sources. The second one to return the instance back to the pool.
    /// </remarks>
    /// <typeparam name="T">>The type the task result.</typeparam>
    public class ValueTaskCompletionSource<T> : IValueTaskSource<T>, IThreadPoolWorkItem
    {
        private readonly Action<object?> cancellationCallback;
        private ManualResetValueTaskSourceCore<T> sourceCore;
        private CancellationTokenRegistration tokenTracker, timeoutTracker;
        private CancellationTokenSource? timeoutSource;
        private bool completed;

        /// <summary>
        /// Initializes a new completion source.
        /// </summary>
        /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
        public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        {
            sourceCore = new() { RunContinuationsAsynchronously = runContinuationsAsynchronously };
            cancellationCallback = CancellationRequested;
            completed = true;
        }

        private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource<T>);

        private void CancellationRequested(object? token)
        {
            if (token is short typedToken)
                CancellationRequested(typedToken);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void CancellationRequested(short token)
        {
            // due to concurrency, this method can be called after Reset or twice
            // that's why we need to skip the call if token doesn't match (call after Reset)
            // or completed flag is set (call twice with the same token)
            if (token == sourceCore.Version && !completed)
            {
                Result<T> result = (timeoutSource?.IsCancellationRequested ?? false) ?
                    OnTimeout() :
                    new(new OperationCanceledException(tokenTracker.Token));

                SetResult(result);
            }
        }

        [CallerMustBeSynchronized]
        private void Recycle()
        {
            tokenTracker.Dispose();
            tokenTracker = default;

            timeoutTracker.Dispose();
            timeoutTracker = default;

            if (timeoutSource is not null)
            {
                // TODO: Attempt to reuse the source with TryReset
                timeoutSource.Dispose();
                timeoutSource = null;
            }
        }

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <param name="result">The value to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TrySetResult(T result)
        {
            if (completed)
                return false;

            SetResult(result);
            return true;
        }

        /// <summary>
        /// Attempts to complete the task sucessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="result">The value to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TrySetResult(short completionToken, T result)
        {
            if (completed || completionToken != sourceCore.Version)
                return false;

            SetResult(result);
            return true;
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
                var error = result.Error;
                if (error is null)
                    sourceCore.SetResult(result.OrDefault());
                else
                    sourceCore.SetException(error);
                completed = true;
            }
        }

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TrySetException(Exception e)
        {
            if (completed)
                return false;

            SetResult(new(e));
            return true;
        }

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="completionToken">The completion token previously obtained from <see cref="Reset(out short, TimeSpan, CancellationToken)"/> method.</param>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TrySetException(short completionToken, Exception e)
        {
            if (completed || completionToken != sourceCore.Version)
                return false;

            SetResult(new(e));
            return true;
        }

        [CallerMustBeSynchronized]
        private void ResetCore()
        {
            sourceCore.Reset();
            completed = false;
        }

        [CallerMustBeSynchronized]
        private ValueTask<T> CreateTaskCore(TimeSpan timeout, CancellationToken token)
        {
            var currentVersion = sourceCore.Version;

            if (timeout == TimeSpan.Zero)
            {
                SetResult(OnTimeout());
                goto exit;
            }

            if (token.IsCancellationRequested)
            {
                SetResult(new(new OperationCanceledException(token)));
                goto exit;
            }

            object? tokenHolder = null;
            if (timeout != InfiniteTimeSpan)
            {
                timeoutSource ??= new();
                tokenHolder ??= currentVersion;
                timeoutTracker = timeoutSource.Token.UnsafeRegister(cancellationCallback, tokenHolder);
                timeoutSource.CancelAfter(timeout);
            }

            if (token.CanBeCanceled)
            {
                tokenHolder ??= currentVersion;
                tokenTracker = token.UnsafeRegister(cancellationCallback, tokenHolder);
            }

            exit:
            return new(this, currentVersion);
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
        /// instance of this class or after (or during) the call of <see cref="AfterConsumed"/> method.
        /// </remarks>
        /// <param name="completionToken">The version of the produced task that can be used later to complete the task without conflicts.</param>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<T> Reset(out short completionToken, TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!completed)
                throw new InvalidOperationException();

            ResetCore();
            completionToken = sourceCore.Version;
            return CreateTaskCore(timeout, token);
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
        /// instance of this class or after (or during) the call of <see cref="AfterConsumed"/> method.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<T> Reset(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!completed)
                throw new InvalidOperationException();

            ResetCore();
            return CreateTaskCore(timeout, token);
        }

        /// <summary>
        /// Attempts to reset state of this object for reuse.
        /// </summary>
        /// <remarks>
        /// Already linked task will never be completed successfully after calling of this method.
        /// </remarks>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Reset()
        {
            Recycle();
            ResetCore();
        }

        /// <summary>
        /// Creates a fresh task linked with this source.
        /// </summary>
        /// <remarks>
        /// This method must be called after <see cref="Reset()"/>.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<T> CreateTask(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            return completed ? new(this, sourceCore.Version) : CreateTaskCore(timeout, token);
        }

        /// <summary>
        /// Invokes when the task is almost completed.
        /// </summary>
        /// <remarks>
        /// This method is called before <see cref="AfterConsumed"/>.
        /// </remarks>
        /// <param name="result">The result of the task.</param>
        protected virtual void BeforeCompleted(Result<T> result)
        {
        }

        /// <summary>
        /// Invokes when this source is ready to reuse.
        /// </summary>
        /// <remarks>
        /// This method is called after <see cref="BeforeCompleted(Result{T})"/>.
        /// </remarks>
        protected virtual void AfterConsumed()
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

        /// <inheritdoc />
        void IThreadPoolWorkItem.Execute() => AfterConsumed();

        /// <inheritdoc />
        T IValueTaskSource<T>.GetResult(short token)
        {
            if (!completed || token != sourceCore.Version)
                throw new InvalidOperationException();

            try
            {
                return sourceCore.GetResult(token);
            }
            finally
            {
                if (IsDerived)
                    ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }
        }

        /// <inheritdoc />
        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
            => sourceCore.GetStatus(token);

        /// <inheritdoc />
        void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => sourceCore.OnCompleted(continuation, state, token, flags);
    }
}
#endif