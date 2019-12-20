using System;
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
        private protected class WaitNode : Synchronizer.WaitNode
        {
            private WaitNode previous;
            private WaitNode next;

            internal WaitNode() => previous = next = null;

            internal WaitNode(WaitNode previous)
                : this()
            {
                previous.next = this;
                this.previous = previous;
            }

            internal void DetachNode()
            {
                if (!(previous is null))
                    previous.next = next;
                if (!(next is null))
                    next.previous = previous;
                next = previous = null;
            }

            internal WaitNode CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }

            internal WaitNode Previous => previous;

            internal WaitNode Next => next;

            internal bool IsRoot => previous is null && next is null;
        }

        private protected interface ILockManager<out N>
            where N : WaitNode
        {
            bool TryAcquire();  //if true then Wait method can be completed synchronously; otherwise, false.

            N CreateNode(WaitNode tail);
        }

        private protected WaitNode head, tail;

        private protected QueuedSynchronizer()
        {
        }

        bool ISynchronizer.HasWaiters => !(head is null);

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

        private async Task<bool> Wait(WaitNode node, TimeSpan timeout, CancellationToken token)
        {
            //cannot use Task.WaitAsync here because this method contains side effect in the form of RemoveNode method
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   //ensure that Delay task is cancelled
                    return true;
                }
            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }
            return await node.Task.ConfigureAwait(false);
        }

        private async Task<bool> Wait(WaitNode node, CancellationToken token)
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
        private protected Task<bool> Wait<M>(ref M manager, TimeSpan timeout, CancellationToken token)
            where M : struct, ILockManager<WaitNode>
        {
            ThrowIfDisposed();
            if (timeout < TimeSpan.Zero && timeout != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            if (manager.TryAcquire())
                return CompletedTask<bool, BooleanConst.True>.Task;
            if (timeout == TimeSpan.Zero)
                return CompletedTask<bool, BooleanConst.False>.Task;    //if timeout is zero fail fast
            if (head is null)
                head = tail = manager.CreateNode(null);
            else
                tail = manager.CreateNode(tail);
            return timeout == InfiniteTimeSpan ?
                token.CanBeCanceled ? Wait(tail, token) : tail.Task
                : Wait(tail, timeout, token);
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
                for (var current = head; !(current is null); current = current.CleanupAndGotoNext())
                    current.TrySetCanceled();
                head = tail = null;
            }
            base.Dispose(disposing);
        }
    }
}