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
        /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> TryAcquire(TimeSpan timeout, CancellationToken token)
        {
            ThrowIfDisposed();
            if(timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            else if (token.IsCancellationRequested)
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

        /// <summary>
        /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryAcquire(TimeSpan timeout) => TryAcquire(timeout, default);

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task Acquire(TimeSpan timeout) => TryAcquire(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task Acquire(CancellationToken token) => TryAcquire(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        /// <summary>
        /// Releases previously acquired exclusive lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            ThrowIfDisposed();
            var waiterTask = head;
            if(!locked)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if(waiterTask is null)
                locked = false;
            else
            {
                RemoveNode(waiterTask);
                waiterTask.Complete();
            }
        }

        /// <summary>
        /// Releases all resources associated with exclusive lock.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe and may not be used concurrently with other members of this instance.
        /// </remarks>
        /// <param name="disposing">Indicates whether the Dispose has been called directly or from Finalizer.</param>
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
