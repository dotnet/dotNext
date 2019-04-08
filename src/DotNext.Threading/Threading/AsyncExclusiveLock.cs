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
    public sealed class AsyncExclusiveLock : AsyncLockBase
    {
        private volatile bool locked;

        /// <summary>
        /// Indicates that exclusive lock taken.
        /// </summary>
        public bool IsLockHeld => locked;

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
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? Wait(tail, timeout, token) : tail.Task;
        }

        /// <summary>
        /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryAcquire(TimeSpan timeout) => TryAcquire(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task Acquire(TimeSpan timeout) => TryAcquire(timeout).CheckOnTimeout();

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task Acquire(CancellationToken token) => TryAcquire(TimeSpan.MaxValue, token).CheckOnTimeout();

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
    }
}
