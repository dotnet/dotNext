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
    public class AsyncReaderWriterLock : Disposable
    {
        private const TaskContinuationOptions CheckOnTimeoutOptions = TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion;

        private sealed class WriteLockNode : AsyncExclusiveLock.LockNode
        {
            private WriteLockNode() : base() {}
            private WriteLockNode(AsyncExclusiveLock.LockNode previous) : base(previous){}

            internal static WriteLockNode Create(AsyncExclusiveLock.LockNode previous) => previous is null ? new WriteLockNode() : new WriteLockNode(previous);
        }

        private sealed class ReadLockNode: AsyncExclusiveLock.LockNode
        {
            internal readonly bool Upgradable;

            private ReadLockNode(bool upgradable) 
                : base()
            {
                Upgradable = upgradable;
            }

            private ReadLockNode(AsyncExclusiveLock.LockNode previous, bool upgradable) 
                : base(previous)
            {
                Upgradable = upgradable;
            }

            internal static ReadLockNode CreateRegular(AsyncExclusiveLock.LockNode previous) => previous is null ? new ReadLockNode(false) : new ReadLockNode(previous, false);

            internal static ReadLockNode CreateUpgradable(AsyncExclusiveLock.LockNode previous) => previous is null ? new ReadLockNode(true) : new ReadLockNode(previous, true);
        }

        //describes internal state of reader/writer lock
        private struct State
        {
            internal long readLocks;
            /*
             * writeLock = false, upgradable = false: regular read lock
             * writeLock = true,  upgradable = true : regular write lock
             * writeLock = false, upgradable = true : upgradable read lock
             * writeLock = true,  upgradable = true : upgraded write lock
             */
            internal bool writeLock;
            internal bool upgradable;
        }

        private delegate bool LockAcquisition(ref State state);

        private AsyncExclusiveLock.LockNode head, tail;
        private State state;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool RemoveNode(AsyncExclusiveLock.LockNode node)
        {
            var inList = ReferenceEquals(head, node) || !node.IsRoot;
            if (ReferenceEquals(head, node))
                head = node.Next;
            if (ReferenceEquals(tail, node))
                tail = node.Previous;
            node.DetachNode();
            return inList;
        }

        private static void CheckOnTimeout(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        private async Task<bool> TryAcquire(AsyncExclusiveLock.LockNode node, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token, default) : new CancellationTokenSource())
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
        private Task<bool> TryEnter(LockAcquisition acquisition, Func<AsyncExclusiveLock.LockNode, AsyncExclusiveLock.LockNode> lockNodeFactory, TimeSpan timeout, CancellationToken token)
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
        /// Gets a value that indicates the recursion policy of the reader/writer lock.
        /// </summary>
        public LockRecursionPolicy RecursionPolicy => LockRecursionPolicy.NoRecursion;

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
        public Task<bool> TryEnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout, default);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterReadLock(CancellationToken token) => TryEnterReadLock(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        /// <summary>
        /// Enters the lock in read mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        private static bool AcquireWriteLock(ref State state)
        {
            if (state.writeLock || state.readLocks > 1L)
                return false;
            else if (state.readLocks == 0L || state.readLocks == 1L && state.upgradable)    //no readers or single upgradable read lock
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
        public Task<bool> TryEnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout, default);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterWriteLock(CancellationToken token) => TryEnterWriteLock(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        /// <summary>
        /// Enters the lock in write mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        private static bool AcquireUpgradableReadLock(ref State state)
        {
            if (state.writeLock || state.upgradable)
                return false;
            else
            {
                state.readLocks++;
                state.upgradable = true;
                return true;
            }
        }

        /// <summary>
        /// Tries to enter the lock in upgradable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns><see langword="true"/> if the caller entered upgradable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradableReadLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireUpgradableReadLock, ReadLockNode.CreateUpgradable, timeout, token);

        /// <summary>
        /// Tries to enter the lock in upgradable mode asynchronously, with an optional time-out.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns><see langword="true"/> if the caller entered upgradable mode; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task<bool> TryEnterUpgradableReadLock(TimeSpan timeout) => TryEnterUpgradableReadLock(timeout, default);
        
        /// <summary>
        /// Enters the lock in upgradable mode asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to abort lock acquisition.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        public Task EnterUpgradableReadLock(CancellationToken token) => TryEnterUpgradableReadLock(TimeSpan.MaxValue, default).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        /// <summary>
        /// Enters the lock in upgradable mode asynchronously.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock.</param>
        /// <returns>The task representing lock acquisition operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
        public Task EnterUpgradableReadLock(TimeSpan timeout) => TryEnterUpgradableReadLock(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        private void ProcessReadLocks()
        {
            if (head is ReadLockNode readLock)
                for (AsyncExclusiveLock.LockNode next; !(readLock is null); readLock = next as ReadLockNode)
                {
                    next = readLock.Next;
                    //remove all read locks and leave upgradable read locks until first write lock
                    if (readLock.Upgradable)
                        if (state.upgradable)    //already in upgradable lock, leave the current node alive
                            continue;
                        else
                            state.upgradable = true;    //enter upgradable read lock
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
        /// <exception cref="SynchronizationLockException">The caller has not entered the lock in upgradable mode.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitUpgradableReadLock()
        {
            ThrowIfDisposed();
            if (state.writeLock || !state.upgradable || state.readLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInUpgradableReadLock);
            state.upgradable = false;
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
            if (state.writeLock || state.readLocks == 1L && state.upgradable || state.readLocks == 0L)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);
            else if (--state.readLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.writeLock = true;
            }
        }

        /// <summary>
        /// Releases all resources associated with reader/writer lock.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe and may not be used concurrently with other members of this instance.
        /// </remarks>
        /// <param name="disposing">Indicates whether the Dispose has been called directly or from Finalizer.</param>
        protected sealed override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (var current = head; !(current is null); current = current.CleanupAndGotoNext())
                    current.TrySetCanceled();
                state = default;
            }
        }
    }
}
