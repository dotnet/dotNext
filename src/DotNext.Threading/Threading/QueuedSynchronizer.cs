using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    /// <summary>
    /// Provides a framework for implementing asynchronous locks and related synchronization primitives that rely on first-in-first-out (FIFO) wait queues.
    /// </summary>
    /// <remarks>
    /// Derived synchronization primitives less efficient in terms of memory pressure when compared with <see cref="Synchronizer">non-queued synchronization primitives</see>.
    /// It provides the individual instance of <see cref="Task{TResult}"/> under contention for each waiter in the queue.
    /// </remarks>
    public abstract class QueuedSynchronizer : Disposable, ISynchronizer
    {
        private protected class WaitNode : ISynchronizer.WaitNode
        {
            private WaitNode? previous;
            private WaitNode? next;

            internal WaitNode()
            {
            }

            internal WaitNode(WaitNode previous)
            {
                previous.next = this;
                this.previous = previous;
            }

            internal void DetachNode()
            {
                if (previous is not null)
                    previous.next = next;
                if (next is not null)
                    next.previous = previous;
                next = previous = null;
            }

            internal WaitNode? CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }

            internal void Append(WaitNode node)
            {
                node.next = next;
                node.previous = this;
                next = node;
            }

            internal void Prepend(WaitNode node)
            {
                node.next = this;
                node.previous = previous;
                previous = node;
            }

            internal WaitNode? Previous => previous;

            internal WaitNode? Next => next;

            internal bool IsNotRoot => previous is not null || next is not null;
        }

        private protected interface ILockManager<TNode>
            where TNode : WaitNode
        {
            bool TryAcquire();  // if true then Wait method can be completed synchronously; otherwise, false.

            TNode CreateNode();
        }

        private sealed class DisposeAsyncNode : WaitNode
        {
            internal DisposeAsyncNode()
            {
            }

            internal DisposeAsyncNode(WaitNode previous)
                : base(previous)
            {
            }
        }

        private Action<double>? contentionCounter, lockDurationCounter;
        private protected WaitNode? first, last;

        private protected QueuedSynchronizer()
        {
        }

        /// <summary>
        /// Sets counter for lock contention.
        /// </summary>
        public IncrementingEventCounter LockContentionCounter
        {
#if NETSTANDARD2_1
            set
#else
            init
#endif
            {
                contentionCounter = (value ?? throw new ArgumentNullException(nameof(value))).Increment;
            }
        }

        /// <summary>
        /// Sets counter of lock duration, in milliseconds.
        /// </summary>
        public EventCounter LockDurationCounter
        {
#if NETSTANDARD2_1
            set
#else
            init
#endif
            {
                lockDurationCounter = (value ?? throw new ArgumentNullException(nameof(value))).WriteMetric;
            }
        }

        /// <inheritdoc/>
        bool ISynchronizer.HasAnticipants => first is not null;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected bool RemoveNode(WaitNode node)
        {
            var inList = false;
            if (ReferenceEquals(first, node))
            {
                first = node.Next;
                inList = true;
            }

            if (ReferenceEquals(last, node))
            {
                last = node.Previous;
                inList = true;
            }

            inList |= node.IsNotRoot;
            node.DetachNode();
            lockDurationCounter?.Invoke(node.Age.TotalMilliseconds);
            return inList;
        }

        private async Task<bool> WaitAsync(WaitNode node, TimeSpan timeout, bool throwOnTimeout, CancellationToken token)
        {
            Debug.Assert(Monitor.IsEntered(this));

            // cannot use Task.WaitAsync here because this method contains side effect in the form of RemoveNode method
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   // ensure that Delay task is cancelled
                    return true;
                }
            }

            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }

            return throwOnTimeout ? throw new TimeoutException() : await node.Task.ConfigureAwait(false);
        }

        private async Task<bool> WaitAsync(WaitNode node, CancellationToken token)
        {
            Debug.Assert(Monitor.IsEntered(this));
            Debug.Assert(token.CanBeCanceled);

            using (var cancellationTask = new CancelableCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously, token))
            {
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, cancellationTask).ConfigureAwait(false)))
                    return true;
            }

            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }

            return await node.Task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected Task<bool> WaitAsync<TNode, TManager>(ref TManager manager, TimeSpan timeout, bool throwOnTimeout, CancellationToken token)
            where TNode : WaitNode
            where TManager : struct, ILockManager<TNode>
        {
            if (IsDisposed)
                return GetDisposedTask<bool>();
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                return Task.FromException<bool>(new ArgumentOutOfRangeException(nameof(timeout)));
            if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            if (manager.TryAcquire())
                return CompletedTask<bool, BooleanConst.True>.Task;
            if (timeout == TimeSpan.Zero)
                return CompletedTask<bool, BooleanConst.False>.Task;    // if timeout is zero fail fast

            var node = manager.CreateNode();
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
            return timeout == InfiniteTimeSpan ?
                token.CanBeCanceled ? WaitAsync(node, token) : node.Task
                : WaitAsync(node, timeout, throwOnTimeout, token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static bool TryAcquire<TNode, TManager>(ref TManager manager)
            where TNode : WaitNode
            where TManager : struct, ILockManager<TNode>
            => manager.TryAcquire();

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
            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                current.TrySetCanceled(token);
            }

            first = last = null;
        }

        private protected bool ProcessDisposeQueue()
        {
            Debug.Assert(Monitor.IsEntered(this));

            if (first is DisposeAsyncNode disposeNode)
            {
                disposeNode.SetResult();
                RemoveNode(disposeNode);
                Dispose();
                return true;
            }

            return false;
        }

        private void NotifyObjectDisposed()
        {
            var e = new ObjectDisposedException(GetType().Name);

            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                if (current is DisposeAsyncNode disposeNode)
                    disposeNode.SetResult();
                else
                    current.TrySetException(e);
            }

            first = last = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static bool IsTerminalNode(WaitNode? node)
            => node is DisposeAsyncNode;

        private Task DisposeAsync()
        {
            Debug.Assert(Monitor.IsEntered(this));

            DisposeAsyncNode disposeNode;
            if (last is null)
                first = last = disposeNode = new DisposeAsyncNode();
            else
                last = disposeNode = new DisposeAsyncNode(last);

            return disposeNode.Task;
        }

        private protected static unsafe ValueTask DisposeAsync<T>(T synchronizer, delegate*<T, bool> lockStateChecker)
            where T : QueuedSynchronizer
        {
            ValueTask result;
            var lockTaken = false;
            try
            {
                Monitor.Enter(synchronizer, ref lockTaken);

                if (lockStateChecker(synchronizer))
                {
                    result = new(synchronizer.DisposeAsync());
                }
                else
                {
                    synchronizer.Dispose();
                    result = new();
                }
            }
            catch (Exception e)
            {
#if NETSTANDARD2_1
                result = new(Task.FromException(e));
#else
                result = ValueTask.FromException(e);
#endif
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(synchronizer);
            }

            return result;
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
            if (disposing)
            {
                NotifyObjectDisposed();
                lockDurationCounter = null;
                lockDurationCounter = null;
            }

            base.Dispose(disposing);
        }
    }
}