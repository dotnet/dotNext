using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents abstract class for family of asynchronous locks.
    /// </summary>
    public abstract class AsyncLockBase: Disposable
    {
        private protected class LockNode : TaskCompletionSource<bool>
        {
            private LockNode previous;
            private LockNode next;

            internal LockNode() : base(TaskCreationOptions.RunContinuationsAsynchronously) => previous = next = null;

            internal LockNode(LockNode previous)
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

            internal LockNode CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }

            internal LockNode Previous => previous;

            internal LockNode Next => next;

            internal bool IsRoot => previous is null && next is null;

            internal void Complete() => SetResult(true);
        }

        private protected LockNode head, tail;

        private protected AsyncLockBase()
        {
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected bool RemoveNode(LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.DetachNode();
            return inList;
        }

        private protected async Task<bool> Wait(LockNode node, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   //ensure that Delay task is cancelled
                    return true;
                }
            }
            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }
            else
                return await node.Task.ConfigureAwait(false);
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
        }
    }
}