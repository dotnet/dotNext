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

        private abstract class LockNode: TaskCompletionSource<bool>
        {
            private LockNode previous;
            private LockNode next;

            private protected LockNode(LockNode previous)
            {
                previous.next = this;
                this.previous = previous;
            }
            
            private protected LockNode() => previous = next = null;

            internal void DetachNode()
            {
                if (!(previous is null))
                    previous.next = next;
                if (!(next is null))
                    next.previous = previous;
                next = previous = null;
            }

            internal LockNode CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }

            internal LockNode Previous => previous;

            internal LockNode Next => next;

            internal bool IsRoot => previous is null && next is null;
            internal void Complete() => SetResult(true);
        }

        private sealed class WriteLockNode : LockNode
        {
            private WriteLockNode() : base() {}
            private WriteLockNode(LockNode previous) : base(previous){}

            internal static WriteLockNode Create(LockNode previous) => previous is null ? new WriteLockNode() : new WriteLockNode(previous);
        }

        private sealed class ReadLockNode: LockNode
        {
            internal readonly bool Upgradable;

            private ReadLockNode(bool upgradable) 
                : base()
            {
                Upgradable = upgradable;
            }

            private ReadLockNode(LockNode previous, bool upgradable) 
                : base(previous)
            {
                Upgradable = upgradable;
            }

            internal static ReadLockNode CreateRegular(LockNode previous) => previous is null ? new ReadLockNode(false) : new ReadLockNode(previous, false);

            internal static ReadLockNode CreateUpgradable(LockNode previous) => previous is null ? new ReadLockNode(true) : new ReadLockNode(previous, true);
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

        private LockNode head, tail;
        private State state;

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

        private static void CheckOnTimeout(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        private async Task<bool> TryAcquire(LockNode node, TimeSpan timeout, CancellationToken token)
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
        private Task<bool> TryEnter(LockAcquisition acquisition, Func<LockNode, LockNode> lockNodeFactory, TimeSpan timeout, CancellationToken token)
        {
            if (token.IsCancellationRequested)
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

        public Task<bool> TryEnterReadLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireReadLock, ReadLockNode.CreateRegular, timeout, token);

        public Task<bool> TryEnterReadLock(TimeSpan timeout) => TryEnterReadLock(timeout, default);

        public Task EnterReadLock(CancellationToken token) => TryEnterReadLock(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

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

        public Task<bool> TryEnterWriteLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireWriteLock, WriteLockNode.Create, timeout, token);

        public Task<bool> TryEnterWriteLock(TimeSpan timeout) => TryEnterWriteLock(timeout, default);

        public Task EnterWriteLock(CancellationToken token) => TryEnterWriteLock(TimeSpan.MaxValue, token).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

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

        public Task<bool> TryEnterUpgradableReadLock(TimeSpan timeout, CancellationToken token) => TryEnter(AcquireUpgradableReadLock, ReadLockNode.CreateUpgradable, timeout, token);

        public Task<bool> TryEnterUpgradableReadLock(TimeSpan timeout) => TryEnterUpgradableReadLock(timeout, default);

        public Task EnterUpgradableReadLock(CancellationToken token) => TryEnterUpgradableReadLock(TimeSpan.MaxValue, default).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        public Task EnterUpgradableReadLock(TimeSpan timeout) => TryEnterUpgradableReadLock(timeout).ContinueWith(CheckOnTimeout, CheckOnTimeoutOptions);

        private void ProcessReadLocks()
        {
            if (head is ReadLockNode readLock)
                for (LockNode next; !(readLock is null); readLock = next as ReadLockNode)
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitUpgradableReadLock()
        {
            if (state.writeLock || !state.upgradable)
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitWriteLock()
        {
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExitReadLock()
        {
            if (state.writeLock || state.readLocks == 1 && state.upgradable)
                throw new SynchronizationLockException(ExceptionMessages.NotInReadLock);
            else if (--state.readLocks == 0L && head is WriteLockNode writeLock)
            {
                RemoveNode(writeLock);
                writeLock.Complete();
                state.writeLock = true;
            }
        }

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
