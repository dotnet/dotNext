using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
        private interface ILockManager<out N> : ILockManager<State, N>
            where N : WaitNode
        {
        }

        private sealed class WriteLockNode : WaitNode
        {
            internal readonly struct LockManager : ILockManager<WriteLockNode>
            {
                WriteLockNode ILockManager<State, WriteLockNode>.CreateNode(WaitNode node) => node is null ? new WriteLockNode() : new WriteLockNode(node);

                bool ILockManager<State, WriteLockNode>.CheckState(ref State state)
                {
                    if (state.writeLock || state.readLocks > 1L)
                        return false;
                    else if (state.readLocks == 0L || state.readLocks == 1L && state.upgreadable)    //no readers or single upgradeable read lock
                    {
                        state.writeLock = true;
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
                ReadLockNode ILockManager<State, ReadLockNode>.CreateNode(WaitNode node) => node is null ? new ReadLockNode(false) : new ReadLockNode(node, false);

                bool ILockManager<State, ReadLockNode>.CheckState(ref State state)
                {
                    if (state.writeLock)
                        return false;
                    else
                    {
                        state.readLocks++;
                        return true;
                    }
                }
            }

            internal readonly struct UpgradeableLockManager : ILockManager<ReadLockNode>
            {
                ReadLockNode ILockManager<State, ReadLockNode>.CreateNode(WaitNode node) => node is null ? new ReadLockNode(true) : new ReadLockNode(node, true);

                bool ILockManager<State, ReadLockNode>.CheckState(ref State state)
                {
                    if (state.writeLock || state.upgreadable)
                        return false;
                    else
                    {
                        state.readLocks++;
                        state.upgreadable = true;
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

        //describes internal state of reader/writer lock
        private struct State
        {
            internal long readLocks;
            /*
             * writeLock = false, upgradeable = false: regular read lock
             * writeLock = true,  upgradeable = true : regular write lock
             * writeLock = false, upgradeable = true : upgradeable read lock
             * writeLock = true,  upgradeable = true : upgraded write lock
             */
            internal volatile bool writeLock;
            internal volatile bool upgreadable;
        }

        private State state;

        /// <summary>
        /// Gets the total number of unique readers.
        /// </summary>
        public long CurrentReadCount => AtomicInt64.VolatileRead(ref state.readLocks);

        /// <summary>
        /// Gets a value that indicates whether the read lock taken.
        /// </summary>
        public bool IsReadLockHeld => CurrentReadCount != 0L;

        /// <summary>
        /// Gets a value that indicates whether the current upgradeable read lock taken.
        /// </summary>
        public bool IsUpgradeableReadLockHeld => state.upgreadable && !state.writeLock;

        /// <summary>
        /// Gets a value that indicates whether the write lock taken.
        /// </summary>
        public bool IsWriteLockHeld => state.writeLock;

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLock(TimeSpan timeout, CancellationToken token) => Wait<State, ReadLockNode.LockManager>(ref state, timeout, token);

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
        public Task EnterReadLock(CancellationToken token) => TryEnterReadLock(TimeSpan.MaxValue, token);

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
        public Task<bool> TryEnterWriteLock(TimeSpan timeout, CancellationToken token) => Wait<State, WriteLockNode.LockManager>(ref state, timeout, token);

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
        public Task EnterWriteLock(CancellationToken token) => TryEnterWriteLock(TimeSpan.MaxValue, token);

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
        public Task<bool> TryEnterUpgradeableReadLock(TimeSpan timeout, CancellationToken token) => Wait<State, ReadLockNode.UpgradeableLockManager>(ref state, timeout, token);

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
        public Task EnterUpgradeableReadLock(CancellationToken token) => TryEnterUpgradeableReadLock(TimeSpan.MaxValue, CancellationToken.None);

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
                        if (state.upgreadable)    //already in upgradeable lock, leave the current node alive
                            continue;
                        else
                            state.upgreadable = true;    //enter upgradeable read lock
                    RemoveNode(readLock);
                    readLock.Complete();
                    state.readLocks += 1L;
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
            if (state.writeLock || !state.upgreadable || state.readLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInUpgradeableReadLock);
            state.upgreadable = false;
            if (--state.readLocks == 0L && head is WriteLockNode writeLock) //no more readers, write lock can be acquired
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.writeLock = true;
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
            if (!state.writeLock)
                throw new SynchronizationLockException(ExceptionMessages.NotInWriteLock);
            else if (head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                return;
            }
            state.writeLock = false;
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
            if (state.writeLock || state.readLocks == 1L && state.upgreadable || state.readLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);
            else if (--state.readLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.writeLock = true;
            }
        }
    }
}
