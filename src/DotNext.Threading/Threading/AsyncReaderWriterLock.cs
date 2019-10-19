using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    /// <remarks>
    /// This lock doesn't support recursion.
    /// </remarks>
    public class AsyncReaderWriterLock : QueuedSynchronizer
    {
        //describes internal state of reader/writer lock
        internal sealed class State
        {
            private long version;  //version of write lock

            //number of acquired read locks
            internal long ReadLocks;
            /*
             * writeLock = false, upgradeable = false: regular read lock
             * writeLock = true,  upgradeable = true : regular write lock
             * writeLock = false, upgradeable = true : upgradeable read lock
             * writeLock = true,  upgradeable = true : upgraded write lock
             */
            internal volatile bool WriteLock;
            internal volatile bool Upgradeable;

            internal State() => version = long.MinValue;

            internal long Version => version.VolatileRead();

            internal void IncrementVersion() => version.IncrementAndGet();
        }

        /// <summary>
        /// Represents lock stamp used for optimistic reading.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct LockStamp : IEquatable<LockStamp>
        {
            private readonly long version;
            private readonly State state;

            internal LockStamp(State state)
            {
                version = state.Version;
                this.state = state;
            }

            /// <summary>
            /// Determines whether the version of internal lock state is not outdated.
            /// </summary>
            public bool IsValid => state != null && state.Version == version;

            /// <summary>
            /// Determines whether this stamp represents the same version of the lock state
            /// as the given stamp.
            /// </summary>
            /// <param name="other">The lock stamp to compare.</param>
            /// <returns><see langword="true"/> of this stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public bool Equals(LockStamp other) => ReferenceEquals(state, other.state) && version == other.version;

            /// <summary>
            /// Determines whether this stamp represents the same version of the lock state
            /// as the given stamp.
            /// </summary>
            /// <param name="other">The lock stamp to compare.</param>
            /// <returns><see langword="true"/> of this stamp is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public override bool Equals(object other) => other is LockStamp stamp && Equals(stamp);

            /// <summary>
            /// Computes hash code for this stamp.
            /// </summary>
            /// <returns>The hash code of this stamp.</returns>
            public override int GetHashCode()
            {
                var hashCode = 1717085722;
                hashCode = hashCode * -1521134295 + version.GetHashCode();
                hashCode = hashCode * -1521134295 + RuntimeHelpers.GetHashCode(state);
                return hashCode;
            }

            /// <summary>
            /// Determines whether the first stamp represents the same version of the lock state
            /// as the second stamp.
            /// </summary>
            /// <param name="first">The first lock stamp to compare.</param>
            /// <param name="second">The second lock stamp to compare.</param>
            /// <returns><see langword="true"/> of <paramref name="first"/> stamp is equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
            public static bool operator ==(in LockStamp first, in LockStamp second)
                => ReferenceEquals(first.state, second.state) && first.version == second.version;

            /// <summary>
            /// Determines whether the first stamp represents the different version of the lock state
            /// as the second stamp.
            /// </summary>
            /// <param name="first">The first lock stamp to compare.</param>
            /// <param name="second">The second lock stamp to compare.</param>
            /// <returns><see langword="true"/> of <paramref name="first"/> stamp is not equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
            public static bool operator !=(in LockStamp first, in LockStamp second)
                => !ReferenceEquals(first.state, second.state) || first.version != second.version;
        }

        private sealed class WriteLockNode : WaitNode
        {
            internal readonly struct LockManager : ILockManager<WriteLockNode>
            {
                private readonly State state;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal LockManager(State state) => this.state = state;

                WriteLockNode ILockManager<WriteLockNode>.CreateNode(WaitNode node) => node is null ? new WriteLockNode() : new WriteLockNode(node);

                public bool TryAcquire()
                {
                    if (state.WriteLock || state.ReadLocks > 1L)
                        return false;
                    else if (state.ReadLocks == 0L || state.ReadLocks == 1L && state.Upgradeable)    //no readers or single upgradeable read lock
                    {
                        state.WriteLock = true;
                        state.IncrementVersion();
                        return true;
                    }
                    else
                        return false;
                }
            }

            private WriteLockNode() : base() { }
            private WriteLockNode(WaitNode previous) : base(previous) { }
        }

        private class ReadLockNode : WaitNode
        {
            internal readonly struct LockManager : ILockManager<ReadLockNode>
            {
                private readonly State state;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal LockManager(State state) => this.state = state;

                ReadLockNode ILockManager<ReadLockNode>.CreateNode(WaitNode node) => node is null ? new ReadLockNode(false) : new ReadLockNode(node, false);

                public bool TryAcquire()
                {
                    if (state.WriteLock)
                        return false;
                    else
                    {
                        state.ReadLocks++;
                        return true;
                    }
                }
            }

            internal readonly struct UpgradeableLockManager : ILockManager<ReadLockNode>
            {
                private readonly State state;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal UpgradeableLockManager(State state) => this.state = state;

                ReadLockNode ILockManager<ReadLockNode>.CreateNode(WaitNode node) => node is null ? new ReadLockNode(true) : new ReadLockNode(node, true);

                public bool TryAcquire()
                {
                    if (state.WriteLock || state.Upgradeable)
                        return false;
                    else
                    {
                        state.ReadLocks++;
                        state.Upgradeable = true;
                        return true;
                    }
                }
            }

            internal readonly bool Upgradeable;

            private protected ReadLockNode(bool upgradeable)
                : base()
            {
                Upgradeable = upgradeable;
            }

            private ReadLockNode(WaitNode previous, bool upgradeable)
                : base(previous)
            {
                Upgradeable = upgradeable;
            }
        }

        private readonly State state;
        private ReadLockNode.LockManager readLock;
        private ReadLockNode.UpgradeableLockManager upgradeableLock;
        private WriteLockNode.LockManager writeLock;

        /// <summary>
        /// Initializes a new reader/writer lock.
        /// </summary>
        public AsyncReaderWriterLock()
        {
            state = new State();
            readLock = new ReadLockNode.LockManager(state);
            upgradeableLock = new ReadLockNode.UpgradeableLockManager(state);
            writeLock = new WriteLockNode.LockManager(state);
        }

        /// <summary>
        /// Gets the total number of unique readers.
        /// </summary>
        public long CurrentReadCount => AtomicInt64.VolatileRead(ref state.ReadLocks);

        /// <summary>
        /// Gets a value that indicates whether the read lock taken.
        /// </summary>
        public bool IsReadLockHeld => CurrentReadCount != 0L;

        /// <summary>
        /// Gets a value that indicates whether the current upgradeable read lock taken.
        /// </summary>
        public bool IsUpgradeableReadLockHeld => state.Upgradeable && !state.WriteLock;

        /// <summary>
        /// Gets a value that indicates whether the write lock taken.
        /// </summary>
        public bool IsWriteLockHeld => state.WriteLock;

        /// <summary>
        /// Returns a stamp that can be validated later.
        /// </summary>
        /// <returns>Optimistic read stamp. May be invalid.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public LockStamp TryOptimisticRead()
            => state.WriteLock ? new LockStamp() : new LockStamp(state);

        /// <summary>
        /// Attempts to acquire write lock without blocking.
        /// </summary>
        /// <param name="stamp">The stamp of the read lock.</param>
        /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterWriteLock(in LockStamp stamp) => stamp.IsValid && writeLock.TryAcquire();

        /// <summary>
        /// Attempts to obtain reader lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterReadLock() => readLock.TryAcquire();

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLock(TimeSpan timeout, CancellationToken token)
            => Wait(ref readLock, timeout, token);

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterReadLock(CancellationToken token) => TryEnterReadLock(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout).CheckOnTimeout();

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterWriteLock(TimeSpan timeout, CancellationToken token)
            => Wait(ref writeLock, timeout, token);

        /// <summary>
        /// Attempts to obtain writer lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterWriteLock() => writeLock.TryAcquire();

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterWriteLock(CancellationToken token) => TryEnterWriteLock(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout).CheckOnTimeout();

        /// <summary>
        /// Tries to enter the lock in upgradeable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradeableReadLock(TimeSpan timeout, CancellationToken token)
            => Wait(ref upgradeableLock, timeout, token);

        /// <summary>
        /// Attempts to obtain upgradeable reader lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterUpgradeableReadLock() => upgradeableLock.TryAcquire();

        /// <summary>
        /// Tries to enter the lock in upgradeable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradeableReadLock(TimeSpan timeout) => TryEnterUpgradeableReadLock(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in upgradeable mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterUpgradeableReadLock(CancellationToken token) => TryEnterUpgradeableReadLock(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in upgradeable mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterUpgradeableReadLock(TimeSpan timeout) => TryEnterUpgradeableReadLock(timeout).CheckOnTimeout();

        private void ProcessReadLocks()
        {
            if (head is ReadLockNode readLock)
                for (WaitNode next; !(readLock is null); readLock = next as ReadLockNode)
                {
                    next = readLock.Next;
                    //remove all read locks and leave upgradeable read locks until first write lock
                    if (readLock.Upgradeable)
                        if (state.Upgradeable)    //already in upgradeable lock, leave the current node alive
                            continue;
                        else
                            state.Upgradeable = true;    //enter upgradeable read lock
                    RemoveNode(readLock);
                    readLock.Complete();
                    state.ReadLocks += 1L;
                }
        }

        /// <summary>
        /// Exits upgradeable mode.
        /// </summary>
        /// <remarks>
        /// Exiting from the lock is synchronous non-blocking operation.
        /// Lock acquisition is an asynchronous operation.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in upgradeable mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitUpgradeableReadLock()
        {
            ThrowIfDisposed();
            if (state.WriteLock || !state.Upgradeable || state.ReadLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInUpgradeableReadLock);
            state.Upgradeable = false;
            if (--state.ReadLocks == 0L && head is WriteLockNode writeLock) //no more readers, write lock can be acquired
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.WriteLock = true;
            }
            else
                ProcessReadLocks();
        }

        /// <summary>
        /// Exits write mode.
        /// </summary>
        /// <remarks>
        /// Exiting from the lock is synchronous non-blocking operation.
        /// Lock acquisition is an asynchronous operation.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in write mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitWriteLock()
        {
            ThrowIfDisposed();
            if (!state.WriteLock)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if (head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                return;
            }
            state.WriteLock = false;
            ProcessReadLocks();
        }

        /// <summary>
        /// Exits read mode.
        /// </summary>
        /// <remarks>
        /// Exiting from the lock is synchronous non-blocking operation.
        /// Lock acquisition is an asynchronous operation.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in read mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitReadLock()
        {
            ThrowIfDisposed();
            if (state.WriteLock || state.ReadLocks == 1L && state.Upgradeable || state.ReadLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);
            else if (--state.ReadLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.WriteLock = true;
            }
        }
    }
}
