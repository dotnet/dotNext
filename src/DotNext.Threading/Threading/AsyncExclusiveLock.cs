using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

    /// <summary>
    /// Represents asynchronous mutually exclusive lock.
    /// </summary>
    public class AsyncExclusiveLock : QueuedSynchronizer, IAsyncDisposable
    {
        [StructLayout(LayoutKind.Auto)]
        private struct State
        {
            private volatile bool state;

            internal readonly bool Value => state;

            internal readonly bool IsLockAllowed => !state;

            internal void AcquireLock() => state = true;

            internal void ExitLock() => state = false;
        }

        private readonly Func<DefaultWaitNode> pool;
        private State state;

        /// <summary>
        /// Initializes a new asynchronous exclusive lock.
        /// </summary>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncExclusiveLock(int concurrencyLevel)
        {
            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            pool = new ConstrainedValueTaskPool<DefaultWaitNode>(concurrencyLevel).Get;
        }

        /// <summary>
        /// Initializes a new asynchronous exclusive lock.
        /// </summary>
        public AsyncExclusiveLock()
        {
            pool = new UnconstrainedValueTaskPool<DefaultWaitNode>().Get;
        }

        /// <summary>
        /// Indicates that exclusive lock taken.
        /// </summary>
        public bool IsLockHeld => state.Value;

        private static bool TryAcquire(ref State state)
        {
            if (state.IsLockAllowed)
            {
                state.AcquireLock();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to obtain exclusive lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAcquire()
        {
            ThrowIfDisposed();
            return TryAcquire(ref state);
        }

        /// <summary>
        /// Tries to enter the lock in exclusive mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered exclusive mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitNoTimeoutAsync(ref state, &TryAcquire, pool, out _, timeout, token);

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask AcquireAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitWithTimeoutAsync(ref state, &TryAcquire, pool, out _, timeout, token);

        /// <summary>
        /// Enters the lock in exclusive mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask AcquireAsync(CancellationToken token = default)
            => AcquireAsync(InfiniteTimeSpan, token);

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

                if (!state.IsLockAllowed)
                    break;

                // skip dead node
                if (current.TrySetResult(true))
                {
                    RemoveNode(current);
                    state.AcquireLock();
                    break;
                }
            }
        }

        /// <summary>
        /// Releases previously acquired exclusive lock.
        /// </summary>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            ThrowIfDisposed();
            if (!state.Value)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            if (IsDisposeRequested)
            {
                Dispose(true);
            }
            else
            {
                DrainWaitQueue();
            }
        }

        private protected sealed override bool IsReadyToDispose => state.Value is false;
    }
}
