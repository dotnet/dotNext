#if !NETSTANDARD2_1
using System;
using System.Runtime.CompilerServices;
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
    /// and <see cref="AfterConsumed"/> methods. The first one to remove the source
    /// from the list of active sources. The second one to return the instance back to the pool.
    /// </remarks>
    /// <typeparam name="T">>The type the task result.</typeparam>
    public class ValueTaskCompletionSource<T> : IValueTaskSource<T>, IThreadPoolWorkItem
    {
        private static readonly ContextCallback ContinuationExecutor = RunInContext;

        private readonly Action<object?> cancellationCallback;
        private readonly bool runContinuationsAsynchronously;
        private readonly object syncRoot;
        private CancellationTokenRegistration tokenTracker, timeoutTracker;
        private CancellationTokenSource? timeoutSource;

        // task management
        private Action<object?>? continuation;
        private object? continuationState, capturedContext;
        private ExecutionContext? context;
        private Result<T> result;
        private short version;
        private volatile bool completed;

        /// <summary>
        /// Initializes a new completion source.
        /// </summary>
        /// <param name="runContinuationsAsynchronously">Indicates that continuations must be executed asynchronously.</param>
        public ValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        {
            syncRoot = new();
            this.runContinuationsAsynchronously = runContinuationsAsynchronously;
            result = new(new InvalidOperationException(ExceptionMessages.CompletionSourceInitialState));
            version = short.MinValue;
            cancellationCallback = CancellationRequested;
            completed = true;
        }

        private static void RunInContext(object? source)
        {
            Debug.Assert(source is ValueTaskCompletionSource<T>);

            Unsafe.As<ValueTaskCompletionSource<T>>(source).InvokeContinuation();
        }

        private static object? CaptureContext()
        {
            var context = SynchronizationContext.Current;
            if (context is null || context.GetType() == typeof(SynchronizationContext))
            {
                var scheduler = TaskScheduler.Current;
                return ReferenceEquals(scheduler, TaskScheduler.Default) ? null : scheduler;
            }

            return context;
        }

        private bool IsDerived => GetType() != typeof(ValueTaskCompletionSource<T>);

        private void CancellationRequested(object? token)
        {
            Debug.Assert(token is short);
            CancellationRequested((short)token);
        }

        // TODO: Add CancellationToken to the signature of this handler in .NET 6
        private void CancellationRequested(short token)
        {
            // due to concurrency, this method can be called after Reset or twice
            // that's why we need to skip the call if token doesn't match (call after Reset)
            // or completed flag is set (call twice with the same token)
            if (!completed)
            {
                lock (syncRoot)
                {
                    if (token == version && !completed)
                    {
                        Result<T> result = (timeoutSource?.IsCancellationRequested ?? false) ?
                            OnTimeout() :
                            OnCanceled(tokenTracker.Token);

                        SetResult(result);
                    }
                }
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
                if (context is null)
                    InvokeContinuation();
                else
                    ExecutionContext.Run(context, ContinuationExecutor, this);
            }
        }

        private static void InvokeContinuation(object? capturedContext, Action<object?> continuation, object? state, bool runAsynchronously)
        {
            switch (capturedContext)
            {
                case null:
                    if (runAsynchronously)
                        ThreadPool.UnsafeQueueUserWorkItem(continuation, state, false);
                    else
                        continuation(state);
                    break;
                case SynchronizationContext context:
                    context.Post(continuation.Invoke, state);
                    break;
                case TaskScheduler scheduler:
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
            }
        }

        private void InvokeContinuation()
        {
            if (continuation is not null)
                InvokeContinuation(capturedContext, continuation, continuationState, runContinuationsAsynchronously);
        }

        [CallerMustBeSynchronized]
        private void ResetCore()
        {
            version += 1;
            completed = false;
            result = default;
            context = null;
            capturedContext = null;
            continuation = null;
            continuationState = null;
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

            // box current token once and only if needed
            object? tokenHolder = null;
            if (timeout != InfiniteTimeSpan)
            {
                timeoutSource ??= new();
                tokenHolder = version;
                timeoutTracker = timeoutSource.Token.UnsafeRegister(cancellationCallback, tokenHolder);
                timeoutSource.CancelAfter(timeout);
            }

            if (token.CanBeCanceled)
            {
                tokenTracker = token.UnsafeRegister(cancellationCallback, tokenHolder ?? version);
            }

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
        /// instance of this class or after (or during) the call of <see cref="AfterConsumed"/> method.
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
        /// instance of this class or after (or during) the call of <see cref="AfterConsumed"/> method.
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
        /// Attempts to reset state of this object for reuse.
        /// </summary>
        /// <remarks>
        /// Already linked task will never be completed successfully after calling of this method.
        /// </remarks>
        public void Reset()
        {
            lock (syncRoot)
            {
                Recycle();
                ResetCore();
            }
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
        void IThreadPoolWorkItem.Execute() => AfterConsumed();

        /// <inheritdoc />
        T IValueTaskSource<T>.GetResult(short token)
        {
            if (!completed || token != version)
                throw new InvalidOperationException();

            if (IsDerived)
                ThreadPool.UnsafeQueueUserWorkItem(this, true);

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

        private void OnCompleted(object? capturedContext, Action<object?> continuation, object? state, short token, bool flowExecutionContext)
        {
            // fast path - monitor lock is not needed
            if (token != version)
                goto invalid_token;

            if (completed)
                goto run_in_place;

            lock (syncRoot)
            {
                // avoid running continuation inside of the lock
                if (token != version)
                    goto invalid_token;

                if (completed)
                    goto run_in_place;

                this.continuation = continuation;
                continuationState = state;
                this.capturedContext = capturedContext;
                this.context = flowExecutionContext ? ExecutionContext.Capture() : null;
                goto exit;
            }

        run_in_place:
            InvokeContinuation(capturedContext, continuation, state, runContinuationsAsynchronously);

        exit:
            return;
        invalid_token:
            throw new InvalidOperationException();
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