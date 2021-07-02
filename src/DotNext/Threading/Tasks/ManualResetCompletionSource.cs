#if !NETSTANDARD2_1
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks
{
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

    /// <summary>
    /// Represents base class for all producers of value tasks.
    /// </summary>
    public abstract class ValueTaskCompletionSourceBase : IThreadPoolWorkItem
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

        private protected ValueTaskCompletionSourceBase(bool runContinuationsAsynchronously)
        {
            this.runContinuationsAsynchronously = runContinuationsAsynchronously;
            syncRoot = new();
            version = short.MinValue;

            // cached callback to avoid further allocations
            cancellationCallback = CancellationRequested;
        }

        private static void RunInContext(object? source)
        {
            Debug.Assert(source is ValueTaskCompletionSourceBase);

            Unsafe.As<ValueTaskCompletionSourceBase>(source).InvokeContinuationCore();
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

        private protected void Configure(TimeSpan timeout, CancellationToken token)
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
        private protected void Recycle()
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
                Recycle();
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

        private protected void OnCompleted(object? capturedContext, Action<object?> continuation, object? state, short token, bool flowExecutionContext)
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
    }
}
#endif