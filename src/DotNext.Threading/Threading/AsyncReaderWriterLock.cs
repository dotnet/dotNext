using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    public class AsyncReaderWriterLock : Disposable
    {
        private abstract class LockNode: TaskCompletionSource<bool>
        {
            private LockNode previous;
            private LockNode next;

            private protected LockNode(LockNode previous)
            {
                previous.next = this;
                this.previous = previous;
            }
            
            private protected LockNode() => previous = next = null;

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

        private sealed class WriteLockNode : LockNode
        {
            internal WriteLockNode() : base() {}
            internal WriteLockNode(LockNode previous) : base(previous){}
        }

        private sealed class ReadLockNode: LockNode
        {
            internal ReadLockNode() : base() { }
            internal ReadLockNode(LockNode previous) : base(previous){ }
        }

        private LockNode head, tail;   
        private long readLocks;
        private bool writeLock;

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
        public Task<bool> TryEnterReadLock(TimeSpan timeout, CancellationToken token)
        {
            if(token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if(!writeLock) //no write locks
            {
                readLocks++;
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else if(timeout == TimeSpan.Zero) //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if(head is null)
                head = tail = new ReadLockNode();
            else 
                tail = new ReadLockNode(tail);
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? TryAcquire(tail, timeout, token) : tail.Task;
        }

        public Task<bool> TryEnterWriteLock(TimeSpan timeout, CancellationToken token)
        {
            if(token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if(!writeLock && readLocks == 0L)
            {
                writeLock = true;
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else if(timeout == TimeSpan.Zero) //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if(head is null)
                head = tail = new WriteLockNode();
            else 
                tail = new WriteLockNode(tail);
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? TryAcquire(tail, timeout, token) : tail.Task;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitWriteLock()
        {
            if(!this.writeLock)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if(head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
            }
            else if(head is ReadLockNode readLock)
                do
                {
                    RemoveNode(readLock);
                    readLock.Complete();
                    readLocks += 1L;
                    readLock = head as ReadLockNode;
                } 
                while(!(readLock is null));
            else
                this.writeLock = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitReadLock()
        {
            if(this.writeLock)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);
            else if(--readLocks == 0L && head is WriteLockNode writeLock)
            {
                this.writeLock = true;
                RemoveNode(writeLock);
                writeLock.Complete();
            }
        }

        protected sealed override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }
    }
}
