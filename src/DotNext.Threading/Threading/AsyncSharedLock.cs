using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

    /// <summary>
    /// Represents a lock that can be acquired in exclusive or weak mode.
    /// </summary>
    /// <remarks>
    /// This lock represents the combination of semaphore and reader-writer
    /// lock. The caller can acquire weak locks simultaneously which count
    /// is limited by the concurrency level passed into the constructor. However, the
    /// only one caller can acquire the lock exclusively.
    /// </remarks>
    public class AsyncSharedLock : QueuedSynchronizer, IAsyncDisposable
    {
        private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<WaitNode>
        {
            private readonly Action<WaitNode> backToPool;
            internal bool IsStrongLock;

            private WaitNode(Action<WaitNode> backToPool) => this.backToPool = backToPool;

            protected override void AfterConsumed() => backToPool(this);

            public static WaitNode CreateSource(Action<WaitNode> backToPool) => new(backToPool);
        }

        [StructLayout(LayoutKind.Auto)]
        private struct State
        {
            private const long ExclusiveMode = -1L;

            internal readonly long ConcurrencyLevel;
            private long remainingLocks;   // -1 means that the lock is acquired in exclusive mode

            internal State(long concurrencyLevel) => ConcurrencyLevel = remainingLocks = concurrencyLevel;

            internal long RemainingLocks
            {
                readonly get => remainingLocks.VolatileRead();
                private set => remainingLocks.VolatileWrite(value);
            }

            internal readonly bool IsWeakLockAllowed => RemainingLocks > 0L;

            internal void AcquireWeakLock() => remainingLocks.DecrementAndGet();

            internal void ExitLock()
            {
                if (RemainingLocks < 0L)
                {
                    RemainingLocks = ConcurrencyLevel;
                }
                else
                {
                    remainingLocks.IncrementAndGet();
                }
            }

            internal readonly bool IsStrongLockHeld => RemainingLocks < 0L;

            internal readonly bool IsStrongLockAllowed => RemainingLocks == ConcurrencyLevel;

            internal void AcquireStrongLock() => RemainingLocks = ExclusiveMode;

            internal void Downgrade() => RemainingLocks = ConcurrencyLevel - 1L;
        }

        private readonly ValueTaskPool<WaitNode> pool;
        private State state;

        /// <summary>
        /// Initializes a new shared lock.
        /// </summary>
        /// <param name="concurrencyLevel">The number of unique callers that can obtain shared lock simultaneously.</param>
        /// <param name="limitedConcurrency">
        /// <see langword="true"/> if the potential number of concurrent flows will not be greater than <paramref name="concurrencyLevel"/>;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
        public AsyncSharedLock(long concurrencyLevel, bool limitedConcurrency = true)
        {
            if (concurrencyLevel < 1L)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            state = new(concurrencyLevel);
            Action<WaitNode> removeFromList = RemoveAndDrainWaitQueue;
            pool = limitedConcurrency
                ? new(concurrencyLevel.Truncate(), removeFromList)
                : new(removeFromList);
        }

        private static void WeakLockControl(ref State state, ref bool flag)
        {
            if (flag)
            {
                state.AcquireWeakLock();
            }
            else
            {
                flag = state.IsWeakLockAllowed;
            }
        }

        private static void StrongLockControl(ref State state, ref bool flag)
        {
            if (flag)
            {
                state.AcquireStrongLock();
            }
            else
            {
                flag = state.IsStrongLockAllowed;
            }
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
        public bool IsLockHeld => state.RemainingLocks < state.ConcurrencyLevel;

        /// <summary>
        /// Indicates that the lock is acquired in exclusive mode.
        /// </summary>
        public bool IsStrongLockHeld => state.IsStrongLockHeld;

        /// <summary>
        /// Attempts to obtain lock synchronously without blocking caller thread.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TryAcquire(bool strongLock)
        {
            ThrowIfDisposed();
            return TryAcquire(ref state, strongLock ? &StrongLockControl : &WeakLockControl);
        }

        /// <summary>
        /// Attempts to enter the lock asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> TryAcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitNoTimeoutAsync(ref state, strongLock ? &StrongLockControl : &WeakLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.IsStrongLock = strongLock;

            return result;
        }

        /// <summary>
        /// Entres the lock asynchronously.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask AcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitWithTimeoutAsync(ref state, strongLock ? &StrongLockControl : &WeakLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.IsStrongLock = strongLock;

            return result;
        }

        /// <summary>
        /// Entres the lock asynchronously.
        /// </summary>
        /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask AcquireAsync(bool strongLock, CancellationToken token = default)
            => AcquireAsync(strongLock, InfiniteTimeSpan, token);

        private protected sealed override void DrainWaitQueue()
        {
            Debug.Assert(Monitor.IsEntered(this));

            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                if (current.IsCompleted)
                {
                    RemoveNode(current);
                    continue;
                }

                switch ((current.IsStrongLock, state.IsStrongLockAllowed))
                {
                    case (true, true):
                        if (current.TrySetResult(true))
                        {
                            RemoveNode(current);
                            state.AcquireStrongLock();
                            return;
                        }

                        continue;
                    case (true, false):
                        return;
                    default:
                        // no more locks to acquire
                        if (!state.IsWeakLockAllowed)
                            return;

                        if (current.TrySetResult(true))
                        {
                            RemoveNode(current);
                            state.AcquireWeakLock();
                        }

                        continue;
                }
            }
        }

        /// <summary>
        /// Releases the acquired weak lock or downgrade exclusive lock to the weak lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Downgrade()
        {
            ThrowIfDisposed();

            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.Downgrade();
            DrainWaitQueue();
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

            if (state.IsStrongLockAllowed) // nothing to release
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            DrainWaitQueue();

            if (IsDisposeRequested && IsReadyToDispose)
                Dispose(true);
        }

        private protected sealed override bool IsReadyToDispose => state.IsStrongLockAllowed && first is null;
    }
}
