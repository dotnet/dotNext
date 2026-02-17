using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading;

using Collections.Concurrent;
using Tasks;

partial struct CancellationTokenMultiplexer
{
    partial class PooledCancellationTokenSource: IValueTaskSource<CancellationToken>, IThreadPoolWorkItem
    {
        private IObjectPool<PooledCancellationTokenSource>? pool;
        private object? callbackOrSentinel, callbackState, schedulingContext;
        private ExecutionContext? context;
        private short version;

        ValueTaskSourceStatus IValueTaskSource<CancellationToken>.GetStatus(short token)
        {
            CheckToken(token);
            return IsCancellationOriginSet
                ? ValueTaskSourceStatus.Succeeded
                : ValueTaskSourceStatus.Pending;
        }

        CancellationToken IValueTaskSource<CancellationToken>.GetResult(short token)
        {
            CheckToken(token);

            var result = CancellationOrigin;
            Debug.Assert(result.IsCancellationRequested);
            Debug.Assert(!IsCancellationRequested);

            DetachLinkedTokens();
            var poolCopy = pool;
            Reset();

            // We don't need to call base.TryReset() because the current CTS remains untouched (see OnCanceled override)
            Debug.Assert(poolCopy is not null);
            version++;
            poolCopy.Return(this);

            return result;
        }

        void IValueTaskSource<CancellationToken>.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            CheckToken(token);
            
            callbackState = state;
            schedulingContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) is not 0
                ? ContinuationHelpers.CaptureSchedulingContext()
                : null;

            context = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) is not 0
                ? ExecutionContext.Capture()
                : null;

            // invoke the continuation in-place, if possible
            if (Interlocked.CompareExchange(ref callbackOrSentinel, continuation, null) is null)
            {
                // nothing to do
            }
            else if (schedulingContext is not null)
            {
                continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
            }
            else
            {
                continuation(state);
            }
        }

        public ValueTask<CancellationToken> CreateTask(IObjectPool<PooledCancellationTokenSource> pool)
        {
            this.pool = pool;
            return new(this, version);
        }

        private protected override void OnCanceled()
        {
            if (pool is null)
            {
                // When pool is null, it's multiplexed token cancellation. Otherwise, it's just a task completion, no need
                // to cancel the root CTS because it cannot be reused (and returned to the pool).
                base.OnCanceled();
            }
            else if (Interlocked.CompareExchange(ref callbackOrSentinel, Sentinel.Instance, null) is not Action<object?> continuation)
            {
                // nothing to do
            }
            else if (schedulingContext is not null)
            {
                continuation.InvokeInExecutionContext(callbackState, schedulingContext, context);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
            }
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (context is not null)
                ExecutionContext.Restore(context); // will be automatically reverted by the ThreadPool internals

            (callbackOrSentinel as Action<object?>)?.Invoke(callbackState);
        }
        
        [StackTraceHidden]
        private void CheckToken(short expectedToken)
        {
            if (version != expectedToken)
                Throw();

            [StackTraceHidden]
            [DoesNotReturn]
            static void Throw() => throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);
        }
    }
}