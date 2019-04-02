using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    public class AsyncReaderWriterLock : Disposable
    {
        private sealed class LockNode : TaskCompletionSource<bool>
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

        private LockNode head, tail;    //write locks
        private long readers;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool RemoveNode(LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.RemoveNode();
            return inList;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAcquireReadLock(CancellationToken token, TimeSpan timeout)
        {

        }

        protected sealed override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }
    }
}
