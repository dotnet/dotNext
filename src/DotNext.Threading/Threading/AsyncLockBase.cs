using System.Threading.Tasks;

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
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
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

        private protected LockNode head, tail;

        private protected AsyncLockBase()
        {
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