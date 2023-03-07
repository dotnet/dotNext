using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks.Pooling;

/// <summary>
/// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
/// </summary>
/// <remarks>
/// This lock doesn't support recursion.
/// </remarks>
[DebuggerDisplay($"Readers = {{{nameof(CurrentReadCount)}}}, WriteLockHeld = {{{nameof(IsWriteLockHeld)}}}")]
public class AsyncReaderWriterLock : QueuedSynchronizer, IAsyncDisposable
{
    private enum LockType : byte
    {
        Read = 0,
        Upgrade,
        Exclusive,
    }

    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>
    {
        internal LockType Type;

        protected override void AfterConsumed() => AfterConsumed(this);

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }
    }

    // describes internal state of reader/writer lock
    [StructLayout(LayoutKind.Auto)]
    internal struct State
    {
        private ulong version;  // version of write lock

        // number of acquired read locks
        private long readLocks; // volatile
        private bool writeLock;

        internal readonly bool WriteLock => Volatile.Read(ref Unsafe.AsRef(in writeLock));

        internal void DowngradeFromWriteLock()
        {
            writeLock = false;
            readLocks = 1L;
        }

        internal void ExitLock()
        {
            if (writeLock)
            {
                writeLock = false;
                readLocks = 0L;
            }
            else
            {
                readLocks--;
            }
        }

        internal readonly long ReadLocks => readLocks.VolatileRead();

        internal readonly ulong Version => version.VolatileRead();

        internal readonly bool IsWriteLockAllowed => writeLock is false && readLocks is 0L;

        internal readonly bool IsUpgradeToWriteLockAllowed => writeLock is false && readLocks is 1L;

        internal void AcquireWriteLock()
        {
            readLocks = 0L;
            writeLock = true;
            version++;
        }

        internal readonly bool IsReadLockAllowed => !writeLock;

        internal void AcquireReadLock() => readLocks++;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ReadLockManager : ILockManager<WaitNode>
    {
        bool ILockManager.IsLockAllowed
            => Unsafe.As<ReadLockManager, State>(ref Unsafe.AsRef(this)).IsReadLockAllowed;

        void ILockManager.AcquireLock()
            => Unsafe.As<ReadLockManager, State>(ref Unsafe.AsRef(this)).AcquireReadLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Read;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct WriteLockManager : ILockManager<WaitNode>
    {
        bool ILockManager.IsLockAllowed
            => Unsafe.As<WriteLockManager, State>(ref Unsafe.AsRef(this)).IsWriteLockAllowed;

        void ILockManager.AcquireLock()
            => Unsafe.As<WriteLockManager, State>(ref Unsafe.AsRef(this)).AcquireWriteLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Exclusive;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UpgradeManager : ILockManager<WaitNode>
    {
        bool ILockManager.IsLockAllowed
            => Unsafe.As<UpgradeManager, State>(ref Unsafe.AsRef(this)).IsUpgradeToWriteLockAllowed;

        void ILockManager.AcquireLock()
            => Unsafe.As<UpgradeManager, State>(ref Unsafe.AsRef(this)).AcquireWriteLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Upgrade;
    }

    /// <summary>
    /// Represents lock stamp used for optimistic reading.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LockStamp : IEquatable<LockStamp>
    {
        private readonly ulong version;
        private readonly bool valid;

        internal LockStamp(in State state)
        {
            version = state.Version;
            valid = true;
        }

        internal bool IsValid(in State state)
            => valid && state.Version == version && !state.WriteLock;

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

    private State state;
    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;

    /// <summary>
    /// Initializes a new reader/writer lock.
    /// </summary>
    /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
    public AsyncReaderWriterLock(int concurrencyLevel)
    {
        if (concurrencyLevel <= 0)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        state = new();
        pool = new(OnCompleted, concurrencyLevel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TLockManager GetLockManager<TLockManager>()
        where TLockManager : struct, ILockManager<WaitNode>
        => ref Unsafe.As<State, TLockManager>(ref state);

    /// <summary>
    /// Initializes a new reader/writer lock.
    /// </summary>
    public AsyncReaderWriterLock()
    {
        state = new();
        pool = new(OnCompleted);
    }

    private void OnCompleted(WaitNode node)
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval && RemoveNode(node))
                DrainWaitQueue();

            pool.Return(node);
        }
    }

    /// <summary>
    /// Gets the total number of unique readers.
    /// </summary>
    public long CurrentReadCount => state.ReadLocks;

    /// <summary>
    /// Gets a value that indicates whether the read lock taken.
    /// </summary>
    public bool IsReadLockHeld => CurrentReadCount is not 0L;

    /// <summary>
    /// Gets a value that indicates whether the write lock taken.
    /// </summary>
    public bool IsWriteLockHeld => state.WriteLock;

    /// <summary>
    /// Returns a stamp that can be validated later.
    /// </summary>
    /// <returns>Optimistic read stamp. May be invalid.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public LockStamp TryOptimisticRead()
    {
        ThrowIfDisposed();

        // Ordering of version and lock state must be respected:
        // Write lock acquisition changes the state to Acquired and then increments the version.
        // Optimistic read lock reads the version and then checks Acquired lock state to avoid false positivies.
        var stamp = new LockStamp(in state);
        return state.WriteLock ? default : stamp;
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
    public bool TryEnterReadLock() => TryEnter<ReadLockManager>();

    /// <summary>
    /// Tries to enter the lock in read mode asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered read mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryEnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref GetLockManager<ReadLockManager>(), new TimeoutAndCancellationToken(timeout, token));

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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask EnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<ReadLockManager>(), new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Enters the lock in read mode asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask EnterReadLockAsync(CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<ReadLockManager>(), new CancellationTokenOnly(token));

    /// <summary>
    /// Attempts to acquire write lock without blocking.
    /// </summary>
    /// <param name="stamp">The stamp of the read lock.</param>
    /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock(in LockStamp stamp)
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            return stamp.IsValid(in state) && TryAcquire(ref GetLockManager<WriteLockManager>());
        }
    }

    /// <summary>
    /// Attempts to obtain writer lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock() => TryEnter<WriteLockManager>();

    /// <summary>
    /// Tries to enter the lock in write mode asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryEnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Enters the lock in write mode asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask EnterWriteLockAsync(CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new CancellationTokenOnly(token));

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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask EnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Tries to upgrade the read lock to the write lock synchronously without blocking of the caller.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryUpgradeToWriteLock() => TryEnter<UpgradeManager>();

    private bool TryEnter<TLockManager>()
        where TLockManager : struct, ILockManager<WaitNode>
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            return TryAcquire(ref GetLockManager<TLockManager>());
        }
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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryUpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref GetLockManager<UpgradeManager>(), new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Upgrades the read lock to the write lock asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask UpgradeToWriteLockAsync(CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<UpgradeManager>(), new CancellationTokenOnly(token));

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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask UpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<UpgradeManager>(), new TimeoutAndCancellationToken(timeout, token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires write lock.
    /// </summary>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered write mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask<bool> TryStealWriteLockAsync(object? reason, TimeSpan timeout, CancellationToken token = default)
        => TryAcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new TimeoutAndInterruptionReasonAndCancellationToken(reason, timeout, token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires write lock.
    /// </summary>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask StealWriteLockAsync(object? reason, TimeSpan timeout, CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new TimeoutAndInterruptionReasonAndCancellationToken(reason, timeout, token));

    /// <summary>
    /// Interrupts all pending callers in the queue and acquires write lock.
    /// </summary>
    /// <param name="reason">The reason for lock steal.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <seealso cref="PendingTaskInterruptedException"/>
    public ValueTask StealWriteLockAsync(object? reason = null, CancellationToken token = default)
        => AcquireAsync(ref pool, ref GetLockManager<WriteLockManager>(), new InterruptionReasonAndCancellationToken(reason, token));

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        Debug.Assert(first is null or WaitNode);

        for (WaitNode? current = Unsafe.As<WaitNode>(first), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or WaitNode);

            next = Unsafe.As<WaitNode>(current.Next);

            switch (current.Type)
            {
                case LockType.Upgrade:
                    if (!state.IsUpgradeToWriteLockAllowed)
                        return;

                    if (RemoveAndSignal(current))
                    {
                        state.AcquireWriteLock();
                        return;
                    }

                    continue;
                case LockType.Exclusive:
                    if (!state.IsWriteLockAllowed)
                        return;

                    // skip dead node
                    if (RemoveAndSignal(current))
                    {
                        state.AcquireWriteLock();
                        return;
                    }

                    continue;
                default:
                    if (!state.IsReadLockAllowed)
                        return;

                    if (RemoveAndSignal(current))
                        state.AcquireReadLock();

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
    public void Release()
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
                Dispose(true);
        }
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
    public void DowngradeFromWriteLock()
    {
        lock (SyncRoot)
        {
            ThrowIfDisposed();

            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.DowngradeFromWriteLock();
            DrainWaitQueue();
        }
    }

    private protected override bool IsReadyToDispose => state.IsWriteLockAllowed && first is null;
}