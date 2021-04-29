using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

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

            internal WaitNode? Previous => previous;

            internal WaitNode? Next => next;

            internal bool IsRoot => previous is null && next is null;
        }

        private protected interface ILockManager<TNode>
            where TNode : WaitNode
        {
            bool TryAcquire();  // if true then Wait method can be completed synchronously; otherwise, false.

            TNode CreateNode(WaitNode? tail);
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

#if !NETSTANDARD2_1
        private ulong contentionCount;
#endif
        private protected WaitNode? head, tail;

        private protected QueuedSynchronizer()
        {
        }

        /// <inheritdoc/>
        bool ISynchronizer.HasAnticipants => head is not null;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected bool RemoveNode(WaitNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.DetachNode();
            return inList;
        }

        [CallerMustBeSynchronized]
        private async Task<bool> WaitAsync(WaitNode node, TimeSpan timeout, CancellationToken token)
        {
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

            return await node.Task.ConfigureAwait(false);
        }

        [CallerMustBeSynchronized]
        private async Task<bool> WaitAsync(WaitNode node, CancellationToken token)
        {
            if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(InfiniteTimeSpan, token)).ConfigureAwait(false)))
                return true;
            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }

            return await node.Task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected Task<bool> WaitAsync<TNode, TManager>(ref TManager manager, TimeSpan timeout, CancellationToken token)
            where TNode : WaitNode
            where TManager : struct, ILockManager<TNode>
        {
            if (IsDisposed)
                return GetDisposedTask<bool>();
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            if (manager.TryAcquire())
                return CompletedTask<bool, BooleanConst.True>.Task;
            if (timeout == TimeSpan.Zero)
                return CompletedTask<bool, BooleanConst.False>.Task;    // if timeout is zero fail fast
            if (tail is null)
                head = tail = manager.CreateNode(null);
            else
                tail = manager.CreateNode(tail);

#if !NETSTANDARD2_1
            Interlocked.Increment(ref contentionCount);
#endif
            return timeout == InfiniteTimeSpan ?
                token.CanBeCanceled ? WaitAsync(tail, token) : tail.Task
                : WaitAsync(tail, timeout, token);
        }

#if !NETSTANDARD2_1
        /// <summary>
        /// Gets the number of times there was contention when trying to take the asynchronous lock.
        /// </summary>
        [CLSCompliant(false)]
        public ulong LockContentionCount => contentionCount.VolatileRead();
#endif

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
            for (WaitNode? current = head, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                current.TrySetCanceled(token);
            }

            head = tail = null;
        }

        [CallerMustBeSynchronized]
        private protected bool ProcessDisposeQueue()
        {
            if (head is DisposeAsyncNode disposeNode)
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

            for (WaitNode? current = head, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                if (current is DisposeAsyncNode disposeNode)
                    disposeNode.SetResult();
                else
                    current.TrySetException(e);
            }

            head = tail = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static bool IsTerminalNode(WaitNode? node)
            => node is DisposeAsyncNode;

        [CallerMustBeSynchronized]
        private Task DisposeAsync()
        {
            DisposeAsyncNode disposeNode;
            if (tail is null)
                head = tail = disposeNode = new DisposeAsyncNode();
            else
                tail = disposeNode = new DisposeAsyncNode(tail);

            return disposeNode.Task;
        }

        private protected static unsafe ValueTask DisposeAsync<T>(T synchronizer, delegate*<T, bool> lockStateChecker)
            where T : QueuedSynchronizer
        {
            ValueTask result;
            Monitor.Enter(synchronizer);
            try
            {
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
            }

            base.Dispose(disposing);
        }
    }
}