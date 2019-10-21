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
        private const long ExclusiveMode = -1L;

        private sealed class StrongLockNode : WaitNode
        {
            internal StrongLockNode() : base() { }
            internal StrongLockNode(WaitNode previous) : base(previous) { }
        }

        private sealed class State
        {
            internal readonly long ConcurrencyLevel;
            internal long RemainingLocks;   //-1 means that the lock is acquired in exclusive mode

            internal State(long concurrencyLevel) => ConcurrencyLevel = RemainingLocks = concurrencyLevel;

            internal long IncrementLocks() => RemainingLocks = RemainingLocks == ExclusiveMode ? ConcurrencyLevel : RemainingLocks + 1L;

            internal bool IsEmpty => RemainingLocks == ConcurrencyLevel;
        }

        private readonly struct StrongLockManager : ILockManager<StrongLockNode>
        {
            private readonly State state;

            internal StrongLockManager(State state) => this.state = state;

            StrongLockNode ILockManager<StrongLockNode>.CreateNode(WaitNode tail) => tail is null ? new StrongLockNode() : new StrongLockNode(tail);

            public bool TryAcquire()
            {
                if (state.RemainingLocks < state.ConcurrencyLevel)
                    return false;
                state.RemainingLocks = ExclusiveMode;
                return true;
            }
        }

        private readonly struct WeakLockManager : ILockManager<WaitNode>
        {
            private readonly State state;

            internal WeakLockManager(State state) => this.state = state;

            WaitNode ILockManager<WaitNode>.CreateNode(WaitNode tail) => tail is null ? new WaitNode() : new WaitNode(tail);

            public bool TryAcquire()
            {
                if (state.RemainingLocks <= 0L)
                    return false;
                state.RemainingLocks -= 1L;
                return true;
            }
        }

        private readonly State state;
        private WeakLockManager weakLock;
        private StrongLockManager strongLock;

        /// <summary>
        /// Initializes a new shared lock.
        /// </summary>
        /// <param name="concurrencyLevel">The number of unique callers that can obtain shared lock simultaneously.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
        public AsyncSharedLock(long concurrencyLevel)
        {
            if (concurrencyLevel < 1L)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
            state = new State(concurrencyLevel);
            weakLock = new WeakLockManager(state);
            strongLock = new StrongLockManager(state);
        }

        /// <summary>
        /// Gets the number of shared locks that can be acquired.
        /// </summary>
        public long RemainingCount => Math.Max(state.RemainingLocks, 0L);

        /// <summary>
        /// Gets the maximum number of locks that can be obtained simultaneously.
        /// </summary>
        public long ConcurrencyLevel => state.ConcurrencyLevel;

        /// <summary>
        /// Indicates that the lock is acquired in exclusive or shared mode.
        /// </summary>
        public bool IsLockHeld => state.RemainingLocks < ConcurrencyLevel;

        /// <summary>
        /// Attempts to obtain lock synchronously without blocking caller thread.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAcquire(bool strongLock)
            => strongLock ? this.strongLock.TryAcquire() : weakLock.TryAcquire();

        /// <summary>
        /// Attempts to enter the lock asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryAcquire(bool strongLock, TimeSpan timeout, CancellationToken token)
            => strongLock ? Wait(ref this.strongLock, timeout, token) : Wait(ref weakLock, timeout, token);

        /// <summary>
        /// Attempts to enter the lock asynchronously, with an optional time-out.
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

        private void ReleasePendingWeakLocks()
        {
            for (WaitNode current = head, next; !(current is null || current is StrongLockNode) && state.RemainingLocks > 0L; state.RemainingLocks--, current = next)
            {
                next = current.Next;
                RemoveNode(current);
                current.Complete();
            }
        }

        /// <summary>
        /// Releases the acquired weak lock or downgrade exclusive lock to the weak lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Downgrade()
        {
            ThrowIfDisposed();
            if (state.IsEmpty)    //nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            state.RemainingLocks = ConcurrencyLevel - 1;
            ReleasePendingWeakLocks();
        }

        /// <summary>
        /// Release the acquired lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            ThrowIfDisposed();
            if (state.IsEmpty)    //nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if (state.IncrementLocks() == ConcurrencyLevel && head is StrongLockNode exclusiveNode)
            {
                RemoveNode(exclusiveNode);
                exclusiveNode.Complete();
                state.RemainingLocks = ExclusiveMode;
            }
            else
                ReleasePendingWeakLocks();
        }
    }
}
