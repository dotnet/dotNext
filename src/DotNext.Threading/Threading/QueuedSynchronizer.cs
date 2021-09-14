using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Tasks;
    using Tasks.Pooling;
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
    /// </summary>
    public class QueuedSynchronizer : Disposable
    {
        private protected abstract class WaitNode : LinkedValueTaskCompletionSource<bool>
        {
            internal bool ThrowOnTimeout;
            private Timestamp createdAt;

            internal void ResetAge() => createdAt = Timestamp.Current;

            protected sealed override Result<bool> OnTimeout() => ThrowOnTimeout ? base.OnTimeout() : false;

            internal TimeSpan Age => createdAt.Elapsed;
        }

        private protected sealed class DefaultWaitNode : WaitNode, IPooledManualResetCompletionSource<DefaultWaitNode>
        {
            private readonly Action<DefaultWaitNode> backToPool;

            private DefaultWaitNode(Action<DefaultWaitNode> backToPool) => this.backToPool = backToPool;

            protected sealed override void AfterConsumed() => backToPool(this);

            static DefaultWaitNode IPooledManualResetCompletionSource<DefaultWaitNode>.CreateSource(Action<DefaultWaitNode> backToPool) => new(backToPool);
        }

        private readonly Action<double>? contentionCounter, lockDurationCounter;
        private readonly TaskCompletionSource disposeTask;
        private protected LinkedValueTaskCompletionSource<bool>? first;
        private LinkedValueTaskCompletionSource<bool>? last;

        private protected QueuedSynchronizer()
        {
            disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected void RemoveAndDrainWaitQueue(WaitNode node)
        {
            if (RemoveNode(node))
                DrainWaitQueue();
        }

        private protected bool IsDisposeRequested
        {
            get;
            private set;
        }

        /// <summary>
        /// Sets counter for lock contention.
        /// </summary>
        public IncrementingEventCounter LockContentionCounter
        {
            init => contentionCounter = (value ?? throw new ArgumentNullException(nameof(value))).Increment;
        }

        /// <summary>
        /// Sets counter of lock duration, in milliseconds.
        /// </summary>
        public EventCounter LockDurationCounter
        {
            init => lockDurationCounter = (value ?? throw new ArgumentNullException(nameof(value))).WriteMetric;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected bool RemoveNode(WaitNode node)
        {
            bool isFirst;
            if (isFirst = ReferenceEquals(first, node))
                first = node.Next;

            if (ReferenceEquals(last, node))
                last = node.Previous;

            node.Detach();
            lockDurationCounter?.Invoke(node.Age.TotalMilliseconds);

            return isFirst;
        }

        private protected virtual void DrainWaitQueue() => Debug.Assert(Monitor.IsEntered(this));

        private void EnqueueNode(WaitNode node, bool throwOnTimeout)
        {
            Debug.Assert(Monitor.IsEntered(this));

            node.ThrowOnTimeout = throwOnTimeout;
            node.ResetAge();

            if (last is null)
            {
                first = last = node;
            }
            else
            {
                last.Append(node);
                last = node;
            }

            contentionCounter?.Invoke(1L);
        }

        /*
         * Lock control function is a special function that allows to check the possibility of transition
         * and perform the transition if needed:
         * static void LockControl(ref State state, ref bool flag)
         * {
         *   if (flag) state.AcquireLock(); else flag = state.IsLockAllowed;
         * }
         */
        private protected unsafe bool TryAcquire<TContext>(ref TContext context, delegate*<ref TContext, ref bool, void> control)
            where TContext : struct
        {
            Debug.Assert(Monitor.IsEntered(this));

            var result = false;
            control(ref context, ref result);
            if (result)
            {
                for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
                {
                    next = current.Next as WaitNode;

                    if (current.IsCompleted)
                    {
                        RemoveNode(current);
                    }
                    else
                    {
                        result = false;
                        goto exit;
                    }
                }

                Debug.Assert(result);
                control(ref context, ref result);
                Debug.Assert(result);
            }

        exit:
            return result;
        }

        private protected unsafe ValueTask WaitWithTimeoutAsync<TContext, TNode>(ref TContext context, delegate*<ref TContext, ref bool, void> control, ValueTaskPool<TNode> pool, out TNode? node, TimeSpan timeout, CancellationToken token)
            where TContext : struct
            where TNode : WaitNode, IPooledManualResetCompletionSource<TNode>
        {
            Debug.Assert(Monitor.IsEntered(this));
            Debug.Assert(control != null);

            node = null;

            if (IsDisposed || IsDisposeRequested)
                return new(DisposedTask);

            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(timeout)));

            if (token.IsCancellationRequested)
                return ValueTask.FromCanceled(token);

            if (TryAcquire(ref context, control))
                return ValueTask.CompletedTask;

            if (timeout == TimeSpan.Zero)
                return ValueTask.FromException(new TimeoutException());

            node = pool.Get();
            EnqueueNode(node, true);

            return node.As<ISupplier<TimeSpan, CancellationToken, ValueTask>>().Invoke(timeout, token);
        }

        private protected unsafe ValueTask<bool> WaitNoTimeoutAsync<TContext, TNode>(ref TContext context, delegate*<ref TContext, ref bool, void> control, ValueTaskPool<TNode> pool, out TNode? node, TimeSpan timeout, CancellationToken token)
            where TContext : struct
            where TNode : WaitNode, IPooledManualResetCompletionSource<TNode>
        {
            Debug.Assert(Monitor.IsEntered(this));
            Debug.Assert(control != null);

            node = null;

            if (IsDisposed || IsDisposeRequested)
                return new(GetDisposedTask<bool>());

            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                return ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));

            if (token.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(token);

            if (TryAcquire(ref context, control))
                return new(true);

            if (timeout == TimeSpan.Zero)
                return new(false);    // if timeout is zero fail fast

            node = pool.Get();
            EnqueueNode(node, false);

            return node.CreateTask(timeout, token);
        }

        /// <summary>
        /// Cancels all suspended callers.
        /// </summary>
        /// <param name="token">The canceled token.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not in canceled state.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void CancelSuspendedCallers(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
                throw new ArgumentOutOfRangeException(nameof(token));

            for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                current.TrySetCanceled(token);
            }

            first = last = null;
        }

        private protected long ResumeSuspendedCallers()
        {
            Debug.Assert(Monitor.IsEntered(this));

            var count = 0L;

            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                if (current.IsCompleted)
                {
                    RemoveNode(current);
                    continue;
                }

                if (current.TrySetResult(true))
                {
                    RemoveNode(current);
                    count += 1L;
                }
            }

            return count;
        }

        private void NotifyObjectDisposed()
        {
            var e = new ObjectDisposedException(GetType().Name);

            for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                current.TrySetException(e);
            }

            first = last = null;
        }

        /// <summary>
        /// Releases all resources associated with exclusive lock.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe and may not be used concurrently with other members of this instance.
        /// </remarks>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            IsDisposeRequested = true;

            if (disposing)
            {
                NotifyObjectDisposed();
                disposeTask.TrySetResult();
            }

            base.Dispose(disposing);
        }

        private protected virtual bool IsReadyToDispose => true;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected override ValueTask DisposeAsyncCore()
        {
            IsDisposeRequested = true;

            if (IsReadyToDispose)
            {
                Dispose(true);
                return ValueTask.CompletedTask;
            }

            return new(disposeTask.Task);
        }

        /// <summary>
        /// Disposes this synchronization primitive gracefully.
        /// </summary>
        /// <returns>The task representing asynchronous result.</returns>
        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }
}