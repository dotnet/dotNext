using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    /// <summary>
    /// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    /// <remarks>
    /// This lock doesn't support recursion.
    /// </remarks>
    public class AsyncReaderWriterLock : AsyncLockBase
    {
        private sealed class WriteLockNode : LockNode
        {
            private WriteLockNode() : base() {}
            private WriteLockNode(LockNode previous) : base(previous){}

            internal static WriteLockNode Create(LockNode previous) => previous is null ? new WriteLockNode() : new WriteLockNode(previous);
        }

        private sealed class ReadLockNode: LockNode
        {
            internal readonly bool Upgradeable;

            private ReadLockNode(bool upgradeable) 
                : base()
            {
                Upgradeable = upgradeable;
            }

            private ReadLockNode(LockNode previous, bool upgradeable) 
                : base(previous)
            {
                Upgradeable = upgradeable;
            }

            internal static ReadLockNode CreateRegular(LockNode previous) => previous is null ? new ReadLockNode(false) : new ReadLockNode(previous, false);

            internal static ReadLockNode CreateUpgradeable(LockNode previous) => previous is null ? new ReadLockNode(true) : new ReadLockNode(previous, true);
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

        private delegate bool LockAcquisition(ref State state);
        private State state;

        /// <summary>
        /// Gets the total number of unique readers.
        /// </summary>
        public long CurrentReadCount => AtomicLong.VolatileGet(ref state.readLocks);

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

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool RemoveNode(LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.DetachNode();
            return inList;
        }

        private async Task<bool> TryAcquire(LockNode node, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   //ensure that Delay task is cancelled
                    return true;
                }
            }
            if (RemoveNode(node))
            {
                token.ThrowIfCancellationRequested();
                return false;
            }
            else
                return await node.Task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task<bool> TryEnter(LockAcquisition acquisition, Func<LockNode, LockNode> lockNodeFactory, TimeSpan timeout, CancellationToken token)
        {
            ThrowIfDisposed();
            if(timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            else if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if (acquisition(ref state)) //no write locks
                return CompletedTask<bool, BooleanConst.True>.Task;
            else if (timeout == TimeSpan.Zero) //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if (head is null)
                head = tail = lockNodeFactory(null);
            else
                tail = lockNodeFactory(tail);
            return timeout < TimeSpan.MaxValue || token.CanBeCanceled ? TryAcquire(tail, timeout, token) : tail.Task;
        }

        private static bool AcquireReadLock(ref State state)
        {
            if (state.writeLock)
                return false;
            else
            {
                state.readLocks++;
                return true;
            }
        }

        /// <summary>
        /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterReadLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireReadLock, ReadLockNode.CreateRegular, timeout, token);

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
        public Task EnterReadLock(CancellationToken token) => TryEnterReadLock(TimeSpan.MaxValue, token).CheckOnTimeout();

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout).CheckOnTimeout();

        private static bool AcquireWriteLock(ref State state)
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

        /// <summary>
        /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterWriteLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireWriteLock, WriteLockNode.Create, timeout, token);

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
        public Task EnterWriteLock(CancellationToken token) => TryEnterWriteLock(TimeSpan.MaxValue, token).CheckOnTimeout();

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout).CheckOnTimeout();

        private static bool AcquireUpgradeableReadLock(ref State state)
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

        /// <summary>
        /// Tries to enter the lock in upgradeable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradeableReadLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireUpgradeableReadLock, ReadLockNode.CreateUpgradeable, timeout, token);

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
        public Task EnterUpgradeableReadLock(CancellationToken token) => TryEnterUpgradeableReadLock(TimeSpan.MaxValue, CancellationToken.None).CheckOnTimeout();

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
                for (LockNode next; !(readLock is null); readLock = next as ReadLockNode)
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
