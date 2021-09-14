using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

    /// <summary>
    /// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    /// <remarks>
    /// This lock doesn't support recursion.
    /// </remarks>
    public class AsyncReaderWriterLock : QueuedSynchronizer, IAsyncDisposable
    {
        private enum LockType : byte
        {
            Read = 0,
            Upgrade,
            Exclusive,
        }

        private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<WaitNode>
        {
            private readonly Action<WaitNode> backToPool;
            internal LockType Type;

            private WaitNode(Action<WaitNode> backToPool) => this.backToPool = backToPool;

            protected override void AfterConsumed() => backToPool(this);

            internal bool IsReadLock => Type != LockType.Exclusive;

            internal bool IsUpgradeableLock => Type == LockType.Upgrade;

            internal bool IsWriteLock => Type == LockType.Exclusive;

            public static WaitNode CreateSource(Action<WaitNode> backToPool) => new(backToPool);
        }

        // describes internal state of reader/writer lock
        [StructLayout(LayoutKind.Auto)]
        internal struct State
        {
            private long version;  // version of write lock

            // number of acquired read locks
            private long readLocks; // volatile
            private volatile bool writeLock;

            internal State(long version)
            {
                this.version = version;
                writeLock = false;
                readLocks = 0L;
            }

            internal readonly bool WriteLock => writeLock;

            internal void DowngradeFromWriteLock()
            {
                writeLock = false;
                ReadLocks = 1L;
            }

            internal void ExitLock()
            {
                if (writeLock)
                {
                    writeLock = false;
                    ReadLocks = 0L;
                }
                else
                {
                    readLocks.DecrementAndGet();
                }
            }

            internal long ReadLocks
            {
                readonly get => readLocks.VolatileRead();
                private set => readLocks.VolatileWrite(value);
            }

            internal readonly long Version => version.VolatileRead();

            internal readonly bool IsWriteLockAllowed => !writeLock && ReadLocks == 0L;

            internal readonly bool IsUpgradeToWriteLockAllowed => !writeLock && ReadLocks == 1L;

            internal void AcquireWriteLock()
            {
                readLocks.VolatileWrite(0L);
                writeLock = true;
                version.IncrementAndGet();
            }

            internal readonly bool IsReadLockAllowed => !writeLock;

            internal void AcquireReadLock() => readLocks.IncrementAndGet();
        }

        /// <summary>
        /// Represents lock stamp used for optimistic reading.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct LockStamp : IEquatable<LockStamp>
        {
            private readonly long version;
            private readonly bool valid;

            internal LockStamp(in State state)
            {
                version = state.Version;
                valid = true;
            }

            internal bool IsValid(in State state)
                => valid && state.Version == version;

            private bool Equals(in LockStamp other) => version == other.version && valid == other.valid;

            /// <summary>
            /// Determines whether this stamp represents the same version of the lock state
            /// as the given stamp.
            /// </summary>
            /// <param name="other">The lock stamp to compare.</param>
            /// <returns><see langword="true"/> of this stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public bool Equals(LockStamp other) => Equals(in other);

            /// <summary>
            /// Determines whether this stamp represents the same version of the lock state
            /// as the given stamp.
            /// </summary>
            /// <param name="other">The lock stamp to compare.</param>
            /// <returns><see langword="true"/> of this stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public override bool Equals([NotNullWhen(true)] object? other) => other is LockStamp stamp && Equals(in stamp);

            /// <summary>
            /// Computes hash code for this stamp.
            /// </summary>
            /// <returns>The hash code of this stamp.</returns>
            public override int GetHashCode() => HashCode.Combine(valid, version);

            /// <summary>
            /// Determines whether the first stamp represents the same version of the lock state
            /// as the second stamp.
            /// </summary>
            /// <param name="first">The first lock stamp to compare.</param>
            /// <param name="second">The second lock stamp to compare.</param>
            /// <returns><see langword="true"/> of <paramref name="first"/> stamp is equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
            public static bool operator ==(in LockStamp first, in LockStamp second)
                => first.Equals(in second);

            /// <summary>
            /// Determines whether the first stamp represents the different version of the lock state
            /// as the second stamp.
            /// </summary>
            /// <param name="first">The first lock stamp to compare.</param>
            /// <param name="second">The second lock stamp to compare.</param>
            /// <returns><see langword="true"/> of <paramref name="first"/> stamp is not equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
            public static bool operator !=(in LockStamp first, in LockStamp second)
                => !first.Equals(in second);
        }

        private readonly ValueTaskPool<WaitNode> pool;
        private State state;

        /// <summary>
        /// Initializes a new reader/writer lock.
        /// </summary>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncReaderWriterLock(int concurrencyLevel)
        {
            if (concurrencyLevel <= 0)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            state = new(long.MinValue);
            pool = new ValueTaskPool<WaitNode>(concurrencyLevel, RemoveAndDrainWaitQueue);
        }

        /// <summary>
        /// Initializes a new reader/writer lock.
        /// </summary>
        public AsyncReaderWriterLock()
        {
            state = new(long.MinValue);
            pool = new(RemoveAndDrainWaitQueue);
        }

        private static void ReadLockControl(ref State state, ref bool flag)
        {
            if (flag)
            {
                state.AcquireReadLock();
            }
            else
            {
                flag = state.IsReadLockAllowed;
            }
        }

        private static void UpgradeToWriteLockControl(ref State state, ref bool flag)
        {
            if (flag)
            {
                state.AcquireWriteLock();
            }
            else
            {
                flag = state.IsUpgradeToWriteLockAllowed;
            }
        }

        private static void WriteLockControl(ref State state, ref bool flag)
        {
            if (flag)
            {
                state.AcquireWriteLock();
            }
            else
            {
                flag = state.IsWriteLockAllowed;
            }
        }

        /// <summary>
        /// Gets the total number of unique readers.
        /// </summary>
        public long CurrentReadCount => state.ReadLocks;

        /// <summary>
        /// Gets a value that indicates whether the read lock taken.
        /// </summary>
        public bool IsReadLockHeld => CurrentReadCount != 0L;

        /// <summary>
        /// Gets a value that indicates whether the write lock taken.
        /// </summary>
        public bool IsWriteLockHeld => state.WriteLock;

        /// <summary>
        /// Returns a stamp that can be validated later.
        /// </summary>
        /// <returns>Optimistic read stamp. May be invalid.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public LockStamp TryOptimisticRead()
        {
            ThrowIfDisposed();
            return state.WriteLock ? new LockStamp() : new LockStamp(in state);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp.
        /// </summary>
        /// <param name="stamp">A stamp to check.</param>
        /// <returns><see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp; else <see langword="false"/>.</returns>
        public bool Validate(in LockStamp stamp) => stamp.IsValid(in state);

        /// <summary>
        /// Attempts to obtain reader lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TryEnterReadLock()
        {
            ThrowIfDisposed();
            return TryAcquire(ref state, &ReadLockControl);
        }

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> TryEnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitNoTimeoutAsync(ref state, &ReadLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Read;

            return result;
        }

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask EnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitWithTimeoutAsync(ref state, &ReadLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Read;

            return result;
        }

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask EnterReadLockAsync(CancellationToken token = default)
            => EnterReadLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Attempts to acquire write lock without blocking.
        /// </summary>
        /// <param name="stamp">The stamp of the read lock.</param>
        /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TryEnterWriteLock(in LockStamp stamp)
        {
            ThrowIfDisposed();
            return stamp.IsValid(in state) && TryAcquire(ref state, &WriteLockControl);
        }

        /// <summary>
        /// Attempts to obtain writer lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TryEnterWriteLock()
        {
            ThrowIfDisposed();
            return TryAcquire(ref state, &WriteLockControl);
        }

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> TryEnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitNoTimeoutAsync(ref state, &WriteLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Exclusive;

            return result;
        }

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask EnterWriteLockAsync(CancellationToken token = default)
            => EnterWriteLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask EnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitWithTimeoutAsync(ref state, &WriteLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Exclusive;

            return result;
        }

        /// <summary>
        /// Tries to upgrade the read lock to the write lock synchronously without blocking of the caller.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TryUpgradeToWriteLock()
        {
            ThrowIfDisposed();
            return TryAcquire(ref state, &UpgradeToWriteLockControl);
        }

        /// <summary>
        /// Tries to upgrade the read lock to the write lock asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> TryUpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitNoTimeoutAsync(ref state, &UpgradeToWriteLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Upgrade;

            return result;
        }

        /// <summary>
        /// Upgrades the read lock to the write lock asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask UpgradeToWriteLockAsync(CancellationToken token = default)
            => UpgradeToWriteLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Upgrades the read lock to the write lock asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask UpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var result = WaitWithTimeoutAsync(ref state, &UpgradeToWriteLockControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Type = LockType.Upgrade;

            return result;
        }

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

                switch (current.Type)
                {
                    case LockType.Upgrade:
                        if (!state.IsUpgradeToWriteLockAllowed)
                            return;

                        if (current.TrySetResult(true))
                        {
                            RemoveNode(current);
                            state.AcquireWriteLock();
                            return;
                        }

                        continue;
                    case LockType.Exclusive:
                        if (!state.IsWriteLockAllowed)
                            return;

                        // skip dead node
                        if (current.TrySetResult(true))
                        {
                            RemoveNode(current);
                            state.AcquireWriteLock();
                            return;
                        }

                        break;
                    default:
                        if (!state.IsReadLockAllowed)
                            return;

                        if (current.TrySetResult(true))
                        {
                            RemoveNode(current);
                            state.AcquireReadLock();
                        }

                        continue;
                }
            }
        }

        /// <summary>
        /// Exits previously acquired mode.
        /// </summary>
        /// <remarks>
        /// Exiting from the lock is synchronous non-blocking operation.
        /// Lock acquisition is an asynchronous operation.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in write mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Release()
        {
            ThrowIfDisposed();

            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            DrainWaitQueue();

            if (IsDisposeRequested && IsReadyToDispose)
                Dispose(true);
        }

        /// <summary>
        /// Downgrades the write lock to the read lock.
        /// </summary>
        /// <remarks>
        /// Exiting from the lock is synchronous non-blocking operation.
        /// Lock acquisition is an asynchronous operation.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in write mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DowngradeFromWriteLock()
        {
            ThrowIfDisposed();

            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.DowngradeFromWriteLock();
            DrainWaitQueue();
        }

        private protected override bool IsReadyToDispose => state.IsWriteLockAllowed && first is null;
    }
}
