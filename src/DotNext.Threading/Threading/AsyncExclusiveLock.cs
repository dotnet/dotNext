using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous mutually exclusive lock.
    /// </summary>
    public sealed class AsyncExclusiveLock : QueuedSynchronizer
    {
        private readonly struct LockManager: ILockManager<bool, WaitNode>
        {
            bool ILockManager<bool, WaitNode>.CheckState(ref bool locked)
            {
                if(locked)
                    return false;
                else
                {
                    locked = true;
                    return true;
                }
            }

            WaitNode ILockManager<bool, WaitNode>.CreateNode(WaitNode tail) => tail is null ? new WaitNode() : new WaitNode(tail);
        }

        private bool locked;

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
        public Task<bool> TryAcquire(TimeSpan timeout, CancellationToken token) => Wait<bool, LockManager>(ref locked, timeout, token);

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
            if(!locked)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if(head is null)
                locked = false;
            else
            {
                head.Complete();
                RemoveNode(head);
            }
        }
    }
}
