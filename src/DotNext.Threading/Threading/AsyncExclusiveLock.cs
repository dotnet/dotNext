using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    /// <summary>
    /// Represents asynchronous mutually exclusive lock.
    /// </summary>
    public sealed class AsyncExclusiveLock : Disposable
    {
        private const TaskContinuationOptions CheckOnTimeoutOptions = TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion;

        internal class LockNode : TaskCompletionSource<bool>
        {
            private LockNode previous;
            private LockNode next;

            internal LockNode() => previous = next = null;

            internal LockNode(LockNode previous)
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
                this.next = this.previous = null;
                return next;
            }

            internal LockNode Previous => previous;

            internal LockNode Next => next;

            internal bool IsRoot => previous is null && next is null;

            internal void Complete() => SetResult(true);
        }

        private LockNode head, tail;
        private bool locked;

        private static void CheckOnTimeout(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool RemoveNode(LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.DetachNode();
            return inList;
        }

        private async Task<bool> TryAcquire(LockNode node, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token, default) : new CancellationTokenSource())
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> TryAcquire(TimeSpan timeout, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if(!locked)    //not locked
            {
                locked = true;
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else if (timeout == TimeSpan.Zero)   //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if(head is null)
                head = tail = new LockNode();
            else
                tail = new LockNode(tail);
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? TryAcquire(tail, timeout, token) : tail.Task;
        }

        public Task<bool> TryAcquire(TimeSpan timeout) => TryAcquire(timeout, default);

        public Task Acquire(TimeSpan timeout) => TryAcquire(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        public Task Acquire(CancellationToken token) => TryAcquire(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            var waiterTask = head;
            if(waiterTask is null)
                locked = false;
            else
            {
                RemoveNode(waiterTask);
                waiterTask.Complete();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (var current = head; !(current is null); current = current.CleanupAndGotoNext())
                    current.TrySetCanceled();
            }
        }
    }
}
