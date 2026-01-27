using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading;

using Collections.Concurrent;
using static Tasks.ContinuationHelpers;

partial struct CancellationTokenMultiplexer
{
    partial class PooledCancellationTokenSource: IValueTaskSource<CancellationToken>, IThreadPoolWorkItem
    {
        private IObjectPool<PooledCancellationTokenSource>? pool;
        private object? callback, callbackState, schedulingContext;
        private ExecutionContext? context;
        private short version;
        
        ValueTaskSourceStatus IValueTaskSource<CancellationToken>.GetStatus(short token)
            => ReferenceEquals(callback, Sentinel.Instance) ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Pending;

        CancellationToken IValueTaskSource<CancellationToken>.GetResult(short token)
        {
            var result = CancellationOrigin;
            Debug.Assert(result.IsCancellationRequested);
            Debug.Assert(!IsCancellationRequested);
            
            DetachLinkedTokens();
            var poolCopy = pool;
            Reset();

            // We don't need to call base.TryReset() because the current CTS remains untouched (see OnCanceled override)
            if (poolCopy is not null)
            {
                version++;
                poolCopy.Return(this);
            }

            return result;
        }

        void IValueTaskSource<CancellationToken>.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            callbackState = state;
            schedulingContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) is not 0
                ? CaptureSchedulingContext()
                : null;

            context = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) is not 0
                ? ExecutionContext.Capture()
                : null;

            if (Interlocked.CompareExchange(ref callback, continuation, null) is null)
            {
                // nothing to do
            }
            else if (schedulingContext is not null)
            {
                continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
            }
            else if (context is not null)
            {
                ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: true);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(continuation, state, preferLocal: true);
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
            else if (Interlocked.CompareExchange(ref callback, Sentinel.Instance, null) is not Action<object?> continuation)
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
            Debug.Assert(callback is Action<object?>);

            // ThreadPool restores the original execution context automatically
            // See https://github.com/dotnet/runtime/blob/cb30e97f8397e5f87adee13f5b4ba914cc2c0064/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPoolWorkQueue.cs#L928
            if (context is not null)
                ExecutionContext.Restore(context);

            Unsafe.As<Action<object?>>(callback).Invoke(callbackState);
        }
    }
}