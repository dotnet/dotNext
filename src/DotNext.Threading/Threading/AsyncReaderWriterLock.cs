using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Runtime;
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    /// <remarks>
    /// This lock doesn't support recursion.
    /// </remarks>
    public class AsyncReaderWriterLock : QueuedSynchronizer, IAsyncDisposable
    {
        private sealed class WriteLockNode : WaitNode
        {
            internal WriteLockNode()
                : base()
            {
            }

            internal WriteLockNode(WaitNode previous)
                : base(previous)
            {
            }
        }

        private class ReadLockNode : WaitNode
        {
            internal readonly bool Upgradeable;

            private protected ReadLockNode(bool upgradeable)
                : base() => Upgradeable = upgradeable;

            private protected ReadLockNode(WaitNode previous, bool upgradeable)
                : base(previous) => Upgradeable = upgradeable;

            internal ReadLockNode()
                : this(false)
            {
            }

            internal ReadLockNode(WaitNode previous)
                : this(previous, false)
            {
            }
        }

        private sealed class UpgradeableReadLockNode : ReadLockNode
        {
            internal UpgradeableReadLockNode()
                : base(true)
            {
            }

            internal UpgradeableReadLockNode(WaitNode previous)
                : base(previous, true)
            {
            }
        }

        // describes internal state of reader/writer lock
        [StructLayout(LayoutKind.Auto)]
        internal struct State : ILockManager<WriteLockNode>, ILockManager<ReadLockNode>, ILockManager<UpgradeableReadLockNode>
        {
            private long version;  // version of write lock

            // number of acquired read locks
            internal long ReadLocks;
            /*
             * writeLock = false, upgradeable = false: regular read lock
             * writeLock = true,  upgradeable = true : regular write lock
             * writeLock = false, upgradeable = true : upgradeable read lock
             * writeLock = true,  upgradeable = true : upgraded write lock
             */
            internal volatile bool WriteLock;
            internal volatile bool Upgradeable;

            internal State(long version)
            {
                this.version = version;
                WriteLock = false;
                Upgradeable = false;
                ReadLocks = 0L;
            }

            internal long Version => version.VolatileRead();

            internal void IncrementVersion() => version.IncrementAndGet();

            // write lock management
            readonly WriteLockNode ILockManager<WriteLockNode>.CreateNode(WaitNode? node) => node is null ? new WriteLockNode() : new WriteLockNode(node);

            bool ILockManager<WriteLockNode>.TryAcquire()
            {
                if (WriteLock || ReadLocks > 1L)
                    return false;

                // no readers or single upgradeable read lock
                if (ReadLocks == 0L || ReadLocks == 1L && Upgradeable)
                {
                    WriteLock = true;
                    IncrementVersion();
                    return true;
                }

                return false;
            }

            // read lock management
            readonly ReadLockNode ILockManager<ReadLockNode>.CreateNode(WaitNode? node) => node is null ? new ReadLockNode() : new ReadLockNode(node);

            bool ILockManager<ReadLockNode>.TryAcquire()
            {
                if (WriteLock)
                    return false;
                ReadLocks++;
                return true;
            }

            // upgradeable read lock management
            readonly UpgradeableReadLockNode ILockManager<UpgradeableReadLockNode>.CreateNode(WaitNode? node) => node is null ? new UpgradeableReadLockNode() : new UpgradeableReadLockNode(node);

            bool ILockManager<UpgradeableReadLockNode>.TryAcquire()
            {
                if (WriteLock || Upgradeable)
                    return false;
                ReadLocks++;
                Upgradeable = true;
                return true;
            }
        }

        /// <summary>
        /// Represents lock stamp used for optimistic reading.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct LockStamp : IEquatable<LockStamp>
        {
            private readonly long version;
            private readonly Box<State> state;

            internal LockStamp(Box<State> state)
            {
                version = state.Value.Version;
                this.state = state;
            }

            /// <summary>
            /// Determines whether the version of internal lock state is not outdated.
            /// </summary>
            public bool IsValid => !state.IsEmpty && state.Value.Version == version;

            private bool Equals(in LockStamp other)
                => state == other.state && version == other.version;

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
            public override bool Equals(object? other) => other is LockStamp stamp && Equals(in stamp);

            /// <summary>
            /// Computes hash code for this stamp.
            /// </summary>
            /// <returns>The hash code of this stamp.</returns>
            public override int GetHashCode() => HashCode.Combine(state, version);

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

        private readonly Box<State> state;

        /// <summary>
        /// Initializes a new reader/writer lock.
        /// </summary>
        public AsyncReaderWriterLock() => state = new Box<State>(new State(long.MinValue));

        /// <summary>
        /// Gets the total number of unique readers.
        /// </summary>
        public long CurrentReadCount => AtomicInt64.VolatileRead(ref state.Value.ReadLocks);

        /// <summary>
        /// Gets a value that indicates whether the read lock taken.
        /// </summary>
        public bool IsReadLockHeld => CurrentReadCount != 0L;

        /// <summary>
        /// Gets a value that indicates whether the current upgradeable read lock taken.
        /// </summary>
        public bool IsUpgradeableReadLockHeld
        {
            get
            {
                ref var currentState = ref state.Value;
                return currentState.Upgradeable && !currentState.WriteLock;
            }
        }

        /// <summary>
        /// Gets a value that indicates whether the write lock taken.
        /// </summary>
        public bool IsWriteLockHeld => state.Value.WriteLock;

        /// <summary>
        /// Returns a stamp that can be validated later.
        /// </summary>
        /// <returns>Optimistic read stamp. May be invalid.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public LockStamp TryOptimisticRead()
        {
            ThrowIfDisposed();
            return state.Value.WriteLock ? new LockStamp() : new LockStamp(state);
        }

        /// <summary>
        /// Attempts to acquire write lock without blocking.
        /// </summary>
        /// <param name="stamp">The stamp of the read lock.</param>
        /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterWriteLock(in LockStamp stamp)
        {
            ThrowIfDisposed();
            return stamp.IsValid && TryAcquire<WriteLockNode, State>(ref state.Value);
        }

        /// <summary>
        /// Attempts to obtain reader lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterReadLock()
        {
            ThrowIfDisposed();
            return TryAcquire<ReadLockNode, State>(ref state.Value);
        }

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLockAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync<ReadLockNode, State>(ref state.Value, timeout, token);

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLockAsync(TimeSpan timeout) => TryEnterReadLockAsync(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterReadLockAsync(CancellationToken token) => TryEnterReadLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterReadLockAsync(TimeSpan timeout, CancellationToken token = default) => TryEnterReadLockAsync(timeout, token).CheckOnTimeout();

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterWriteLockAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync<WriteLockNode, State>(ref state.Value, timeout, token);

        /// <summary>
        /// Attempts to obtain writer lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterWriteLock()
        {
            ThrowIfDisposed();
            return TryAcquire<WriteLockNode, State>(ref state.Value);
        }

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterWriteLockAsync(TimeSpan timeout) => TryEnterWriteLockAsync(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterWriteLockAsync(CancellationToken token) => TryEnterWriteLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default) => TryEnterWriteLockAsync(timeout, token).CheckOnTimeout();

        /// <summary>
        /// Tries to enter the lock in upgradeable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradeableReadLockAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync<UpgradeableReadLockNode, State>(ref state.Value, timeout, token);

        /// <summary>
        /// Attempts to obtain upgradeable reader lock synchronously without blocking caller thread.
        /// </summary>
        /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryEnterUpgradeableReadLock()
        {
            ThrowIfDisposed();
            return TryAcquire<UpgradeableReadLockNode, State>(ref state.Value);
        }

        /// <summary>
        /// Tries to enter the lock in upgradeable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradeableReadLockAsync(TimeSpan timeout) => TryEnterUpgradeableReadLockAsync(timeout, CancellationToken.None);

        /// <summary>
        /// Enters the lock in upgradeable mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterUpgradeableReadLockAsync(CancellationToken token) => TryEnterUpgradeableReadLockAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Enters the lock in upgradeable mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterUpgradeableReadLockAsync(TimeSpan timeout, CancellationToken token = default) => TryEnterUpgradeableReadLockAsync(timeout, token).CheckOnTimeout();

        [CallerMustBeSynchronized]
        private void ProcessReadLocks()
        {
            var readLock = head as ReadLockNode;
            ref var currentState = ref state.Value;
            for (WaitNode? next; readLock is not null; readLock = next as ReadLockNode)
            {
                next = readLock.Next;

                // remove all read locks and leave upgradeable read locks until first write lock
                if (readLock.Upgradeable)
                {
                    if (currentState.Upgradeable) // already in upgradeable lock, leave the current node alive
                        continue;
                    else
                        currentState.Upgradeable = true;    // enter upgradeable read lock
                }

                RemoveNode(readLock);
                readLock.Complete();
                currentState.ReadLocks += 1L;

                if (IsTerminalNode(next))
                    break;
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
            ref var currentState = ref state.Value;
            if (currentState.WriteLock || !currentState.Upgradeable || currentState.ReadLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInUpgradeableReadLock);
            if (ProcessDisposeQueue())
                return;

            currentState.Upgradeable = false;

            // no more readers, write lock can be acquired
            if (--currentState.ReadLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                currentState.WriteLock = true;
            }
            else
            {
                ProcessReadLocks();
            }
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
            ref var currentState = ref state.Value;
            if (!currentState.WriteLock)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            if (ProcessDisposeQueue())
                return;

            if (head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
            }
            else
            {
                currentState.WriteLock = false;
                ProcessReadLocks();
            }
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
            ref var currentState = ref state.Value;
            if (currentState.WriteLock || currentState.ReadLocks == 1L && currentState.Upgradeable || currentState.ReadLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);

            if (!ProcessDisposeQueue() && --currentState.ReadLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                currentState.WriteLock = true;
            }
        }

        /// <summary>
        /// Disposes this lock asynchronously and gracefully.
        /// </summary>
        /// <remarks>
        /// If this lock is not acquired then the method just completes synchronously.
        /// Otherwise, it waits for calling of <see cref="ExitReadLock"/>,  method.
        /// </remarks>
        /// <returns>The task representing graceful shutdown of this lock.</returns>
        public unsafe ValueTask DisposeAsync()
        {
            return IsDisposed ? new ValueTask() : DisposeAsync(this, &IsLockHeld);

            static bool IsLockHeld(AsyncReaderWriterLock rwLock) => rwLock.IsReadLockHeld || rwLock.IsWriteLockHeld;
        }
    }
}
