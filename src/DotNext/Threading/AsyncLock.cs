using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    public sealed class AsyncLock: Disposable
    {
        private sealed class LockNode: TaskCompletionSource<bool>
        {
            private LockNode previous;
            private LockNode next;

            internal LockNode() => previous = next = null;

            internal LockNode(LockNode previous)
            {
                previous.next = this;
                this.previous = previous;
            }

            internal void RemoveNode()
            {
                if(!(previous is null))
                    previous.next = next;
                if(!(next is null))
                    next.previous = previous;
                next = previous = null;
            }

            internal LockNode CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = this.previous = null;
                return next;
            }

            internal LockNode Previous => previous;

            internal LockNode Next => next;

            internal bool IsRoot => previous is null && next is null;

            internal void Complete() => SetResult(true);
        }

        private LockNode head, tail;

        private LockNode NewLockNode() => head is null ? head = tail = new LockNode() : tail = new LockNode(tail);

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool RemoveNode(LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if(ReferenceEquals(head, node))
                head = node.Next;
            if(ReferenceEquals(tail, node))
                tail = node.Previous;
            node.RemoveNode();
            return inList;
        }

        private async Task<bool> TryAcquire(LockNode node, CancellationToken token, Timeout timeout)
        {
            using(var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token, default) : new CancellationTokenSource())
            {
                if(ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   //ensure that Delay task is cancelled
                    return true;
                }
            }
            if(RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }
            else
                return await node.Task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task<bool> TryAcquire(CancellationToken token, TimeSpan timeout)
        {
            if(head is null)
            {
                head = tail = new LockNode();
                return Task.FromResult(true);
            }
            else
            {
                tail = new LockNode(tail);
                return TryAcquire(tail, token, new Timeout(timeout));
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            var tail = this.tail;
            RemoveNode(tail);
            tail.Complete();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                for(var current = head; !(current is null); current = current.CleanupAndGotoNext())
                    current.TrySetCanceled();
            }
        }
    }
}