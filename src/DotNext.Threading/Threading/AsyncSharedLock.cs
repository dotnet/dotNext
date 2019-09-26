using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents a lock that can be acquired in exclusive or weak mode.
    /// </summary>
    /// <remarks>
    /// This lock represents the combination of semaphore and reader-writer
    /// lock. The caller can acquire weak locks simultaneously which count
    /// is limited by the concurrency level passed into the constructor. However, the
    /// only one caller can acquire the lock exclusively.
    /// </remarks>
    public class AsyncSharedLock : QueuedSynchronizer
    {
        private sealed class StrongLockNode : WaitNode
        {
            internal StrongLockNode() : base() { }
            internal StrongLockNode(WaitNode previous) : base(previous) { }
        }

        internal readonly struct StrongLockManager : ILockManager<StrongLockNode>
        {
            private readonly AsyncSharedLock state;

            internal StrongLockManager(AsyncSharedLock state) => this.state = state;

            StrongLockNode ILockManager<StrongLockNode>.CreateNode(WaitNode tail) => tail is null ? new StrongLockNode() : new StrongLockNode(tail);

            bool ILockManager<StrongLockNode>.TryAcquire()
            {
                if (state.remainingLocks < state.ConcurrencyLevel)
                    return false;
                state.remainingLocks = ExclusiveMode;
                return true;
            }
        }

        private readonly struct WeakLockManager : ILockManager<WaitNode>
        {
            private readonly AsyncSharedLock state;

            internal WeakLockManager(AsyncSharedLock state) => this.state = state;

            WaitNode ILockManager<WaitNode>.CreateNode(WaitNode tail) => tail is null ? new WaitNode() : new WaitNode(tail);

            bool ILockManager<WaitNode>.TryAcquire()
            {
                if (state.remainingLocks <= 0L)
                    return false;
                state.remainingLocks -= 1L;
                return true;
            }
        }

        private const long ExclusiveMode = -1L;
        private long remainingLocks;    //-1 means that the lock is acquired in exclusive mode

        /// <summary>
        /// Initializes a new shared lock.
        /// </summary>
        /// <param name="concurrencyLevel">The number of unique callers that can obtain shared lock simultaneously.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
        public AsyncSharedLock(long concurrencyLevel)
        {
            if (concurrencyLevel < 1L)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
            ConcurrencyLevel = remainingLocks = concurrencyLevel;
        }

        /// <summary>
        /// Gets the number of shared locks that can be acquired.
        /// </summary>
        public long RemainingCount => Math.Max(remainingLocks, 0L);

        /// <summary>
        /// Gets the maximum number of locks that can be obtained simultaneously.
        /// </summary>
        public long ConcurrencyLevel { get; }

        /// <summary>
        /// Indicates that the lock is acquired in exclusive or shared mode.
        /// </summary>
        public bool IsLockHeld => remainingLocks < ConcurrencyLevel;

        /// <summary>
        /// Tries to enter the lock asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryAcquire(bool strongLock, TimeSpan timeout, CancellationToken token)
        {
            if (strongLock)
            {
                var lockManager = new StrongLockManager(this);
                return Wait(ref lockManager, timeout, token);
            }
            else
            {
                var lockManager = new WeakLockManager(this);
                return Wait(ref lockManager, timeout, token);
            }
        }

        /// <summary>
        /// Tries to enter the lock asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryAcquire(bool strongLock, TimeSpan timeout) => TryAcquire(strongLock, timeout, CancellationToken.None);

        /// <summary>
        /// Entres the lock asynchronously.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task Acquire(bool strongLock, TimeSpan timeout) => TryAcquire(strongLock, timeout).CheckOnTimeout();

        /// <summary>
        /// Entres the lock asynchronously.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task Acquire(bool strongLock, CancellationToken token) => TryAcquire(strongLock, InfiniteTimeSpan, token);

        private long IncrementLocks() => remainingLocks = remainingLocks == ExclusiveMode ? ConcurrencyLevel : remainingLocks + 1L;

        /// <summary>
        /// Release the acquired lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            ThrowIfDisposed();
            if (remainingLocks == ConcurrencyLevel)    //nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if (IncrementLocks() == ConcurrencyLevel && head is StrongLockNode exclusiveNode)
            {
                RemoveNode(exclusiveNode);
                exclusiveNode.Complete();
                remainingLocks = ExclusiveMode;
            }
            else for (WaitNode current = head, next; !(current is null || current is StrongLockNode) && remainingLocks > 0L; remainingLocks--, current = next)
                {
                    next = current.Next;
                    RemoveNode(current);
                    current.Complete();
                }
        }
    }
}
