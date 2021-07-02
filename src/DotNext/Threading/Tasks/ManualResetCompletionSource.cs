#if !NETSTANDARD2_1
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;
using ValueTaskSourceOnCompletedFlags = System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags;

namespace DotNext.Threading.Tasks
{
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

    /// <summary>
    /// Represents base class for producer of value task.
    /// </summary>
    public abstract class ManualResetCompletionSource : IThreadPoolWorkItem
    {
        private static readonly ContextCallback ContinuationExecutor = RunInContext;

        private readonly Action<object?> cancellationCallback;
        private readonly bool runContinuationsAsynchronously;
        private protected readonly object syncRoot;
        private CancellationTokenRegistration tokenTracker, timeoutTracker;
        private CancellationTokenSource? timeoutSource;

        // task management
        private Action<object?>? continuation;
        private object? continuationState, capturedContext;
        private ExecutionContext? context;
        private protected short version;
        private protected volatile bool completed;

        private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
        {
            this.runContinuationsAsynchronously = runContinuationsAsynchronously;
            syncRoot = new();
            version = short.MinValue;

            // cached callback to avoid further allocations
            cancellationCallback = CancellationRequested;
        }

        private static void RunInContext(object? source)
        {
            Debug.Assert(source is ManualResetCompletionSource);

            Unsafe.As<ManualResetCompletionSource>(source).InvokeContinuationCore();
        }

        private void CancellationRequested(object? token)
        {
            Debug.Assert(token is short);
            CancellationRequested((short)token);
        }

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
                        if (timeoutSource?.IsCancellationRequested ?? false)
                            CompleteAsTimedOut();
                        else
                            CompleteAsCanceled(tokenTracker.Token);
                    }
                }
            }
        }

        private protected void StartTrackingCancellation(TimeSpan timeout, CancellationToken token)
        {
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
        }

        private protected abstract void CompleteAsTimedOut();

        private protected abstract void CompleteAsCanceled(CancellationToken token);

        private protected static object? CaptureContext()
        {
            var context = SynchronizationContext.Current;
            if (context is null || context.GetType() == typeof(SynchronizationContext))
            {
                var scheduler = TaskScheduler.Current;
                return ReferenceEquals(scheduler, TaskScheduler.Default) ? null : scheduler;
            }

            return context;
        }

        [CallerMustBeSynchronized]
        private protected void StopTrackingCancellation()
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

        private void InvokeContinuationCore()
        {
            if (continuation is not null)
                InvokeContinuation(capturedContext, continuation, continuationState, runContinuationsAsynchronously);
        }

        private protected void InvokeContinuation()
        {
            if (context is null)
                InvokeContinuationCore();
            else
                ExecutionContext.Run(context, ContinuationExecutor, this);
        }

        [CallerMustBeSynchronized]
        private protected virtual void ResetCore()
        {
            version += 1;
            completed = false;
            context = null;
            capturedContext = null;
            continuation = null;
            continuationState = null;
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
                StopTrackingCancellation();
                ResetCore();
            }
        }

        /// <summary>
        /// Invokes when this source is ready to reuse.
        /// </summary>
        protected virtual void AfterConsumed()
        {
        }

        /// <inheritdoc />
        void IThreadPoolWorkItem.Execute() => AfterConsumed();

        private protected void QueueAfterConsumed()
            => ThreadPool.UnsafeQueueUserWorkItem(this, true);

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
                context = flowExecutionContext ? ExecutionContext.Capture() : null;
                goto exit;
            }

        run_in_place:
            InvokeContinuation(capturedContext, continuation, state, runContinuationsAsynchronously);

        exit:
            return;
        invalid_token:
            throw new InvalidOperationException();
        }

        private protected void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            var capturedContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == 0 ? null : CaptureContext();
            OnCompleted(capturedContext, continuation, state, token, (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0);
        }

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="e">The exception to be returned to the consumer.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public abstract bool TrySetException(Exception e);

        /// <summary>
        /// Attempts to complete the task unsuccessfully.
        /// </summary>
        /// <param name="token">The canceled token.</param>
        /// <returns><see langword="true"/> if the result is completed successfully; <see langword="false"/> if the task has been canceled or timed out.</returns>
        public abstract bool TrySetCanceled(CancellationToken token);
    }

    /// <summary>
    /// Represents base class for producer of value task.
    /// </summary>
    /// <typeparam name="T">The type of value task.</typeparam>
    public abstract class ManualResetCompletionSource<T> : ManualResetCompletionSource
        where T : struct, IEquatable<T>
    {
        private protected ManualResetCompletionSource(bool runContinuationsAsynchronously)
            : base(runContinuationsAsynchronously)
        {
        }

        private protected abstract T Task { get; }

        [CallerMustBeSynchronized]
        private T CreateTaskCore(TimeSpan timeout, CancellationToken token)
        {
            if (timeout == TimeSpan.Zero)
            {
                CompleteAsTimedOut();
                goto exit;
            }

            if (token.IsCancellationRequested)
            {
                CompleteAsCanceled(token);
                goto exit;
            }

            StartTrackingCancellation(timeout, token);

        exit:
            return Task;
        }

        /// <summary>
        /// Resets the state of the underlying task and return a fresh incompleted task.
        /// </summary>
        /// <remarks>
        /// The returned task can be completed in a three ways: through cancellation token, timeout
        /// or by calling <c>TrySetException</c> or <c>TrySetResult</c>.
        /// If <paramref name="timeout"/> is <see cref="InfiniteTimeSpan"/> then this source doesn't
        /// track the timeout. If <paramref name="token"/> is not cancelable then this source
        /// doesn't track the cancellation. If both conditions are met then this source doesn't allocate
        /// additional memory on the heap. Otherwise, the allocation is very minimal and needed
        /// for cancellation registrations.
        /// This method can be called safely in the following circumstances: after construction of a new
        /// instance of this class or after (or during) the call of <see cref="ManualResetCompletionSource.AfterConsumed"/> method.
        /// </remarks>
        /// <param name="completionToken">The version of the produced task that can be used later to complete the task without conflicts.</param>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        public T Reset(out short completionToken, TimeSpan timeout, CancellationToken token)
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
        /// or by calling <c>TrySetException</c> or <c>TrySetResult</c>.
        /// If <paramref name="timeout"/> is <see cref="InfiniteTimeSpan"/> then this source doesn't
        /// track the timeout. If <paramref name="token"/> is not cancelable then this source
        /// doesn't track cancellation. If both conditions are met then this source doesn't allocate
        /// additional memory on the heap. Otherwise, the allocation is very minimal and needed
        /// for cancellation registrations.
        /// This method can be called safely in the following circumstances: after construction of a new
        /// instance of this class or after (or during) the call of <see cref="ManualResetCompletionSource.AfterConsumed"/> method.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">The task was requested but not yet completed.</exception>
        public T Reset(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!completed)
                throw new InvalidOperationException();

            T result;
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
        /// This method must be called after <see cref="ManualResetCompletionSource.Reset()"/>.
        /// </remarks>
        /// <param name="timeout">The timeout associated with the task.</param>
        /// <param name="token">The cancellation token that can be used to cancel the task.</param>
        /// <returns>A fresh incompleted task.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is les than zero but not equals to <see cref="InfiniteTimeSpan"/>.</exception>
        public T CreateTask(TimeSpan timeout, CancellationToken token)
        {
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            T result;

            if (completed)
            {
                result = Task;
            }
            else
            {
                lock (syncRoot)
                {
                    result = completed ? Task : CreateTaskCore(timeout, token);
                }
            }

            return result;
        }
    }
}
#endif