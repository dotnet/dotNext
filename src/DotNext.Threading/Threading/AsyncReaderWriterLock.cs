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
        private Action<WaitNode>? consumedCallback;
        internal LockType Type;

        protected override void AfterConsumed() => AfterConsumed(this);

        ref Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed => ref consumedCallback;

        internal bool IsReadLock => Type != LockType.Exclusive;

        internal bool IsUpgradeableLock => Type == LockType.Upgrade;

        internal bool IsWriteLock => Type == LockType.Exclusive;
    }

    // describes internal state of reader/writer lock
    internal sealed class State
    {
        private long version;  // version of write lock

        // number of acquired read locks
        private long readLocks; // volatile
        private volatile bool writeLock;

        internal State()
        {
            version = long.MinValue;
            writeLock = false;
            readLocks = 0L;
        }

        internal bool WriteLock => writeLock;

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
            get => readLocks.VolatileRead();
            private set => readLocks.VolatileWrite(value);
        }

        internal long Version => version.VolatileRead();

        internal bool IsWriteLockAllowed => !writeLock && ReadLocks == 0L;

        internal bool IsUpgradeToWriteLockAllowed => !writeLock && ReadLocks == 1L;

        internal void AcquireWriteLock()
        {
            readLocks.VolatileWrite(0L);
            writeLock = true;
            version.IncrementAndGet();
        }

        internal bool IsReadLockAllowed => !writeLock;

        internal void AcquireReadLock() => readLocks.IncrementAndGet();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ReadLockManager : ILockManager<WaitNode>
    {
        private readonly State state;

        internal ReadLockManager(State state) => this.state = state;

        bool ILockManager.IsLockAllowed => state.IsReadLockAllowed;

        void ILockManager.AcquireLock() => state.AcquireReadLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Read;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct WriteLockManager : ILockManager<WaitNode>
    {
        private readonly State state;

        internal WriteLockManager(State state) => this.state = state;

        bool ILockManager.IsLockAllowed => state.IsWriteLockAllowed;

        void ILockManager.AcquireLock() => state.AcquireWriteLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Exclusive;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UpgradeManager : ILockManager<WaitNode>
    {
        private readonly State state;

        internal UpgradeManager(State state) => this.state = state;

        bool ILockManager.IsLockAllowed => state.IsUpgradeToWriteLockAllowed;

        void ILockManager.AcquireLock() => state.AcquireWriteLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.Type = LockType.Upgrade;
    }

    /// <summary>
    /// Represents lock stamp used for optimistic reading.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LockStamp : IEquatable<LockStamp>
    {
        private readonly long version;
        private readonly bool valid;

        internal LockStamp(State state)
        {
            version = state.Version;
            valid = true;
        }

        internal bool IsValid(State state)
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

    private readonly State state;
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

    /// <summary>
    /// Initializes a new reader/writer lock.
    /// </summary>
    public AsyncReaderWriterLock()
    {
        state = new();
        pool = new(OnCompleted);
    }

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(WaitNode node)
    {
        if (node.NeedsRemoval && RemoveNode(node))
            DrainWaitQueue();

        pool.Return(node);
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
    public LockStamp TryOptimisticRead()
    {
        ThrowIfDisposed();

        // Ordering of version and lock state must be respected:
        // Write lock acquisition changes the state to Acquired and then increments the version.
        // Optimistic read lock reads the version and then checks Acquired lock state to avoid false positivies.
        var stamp = new LockStamp(state);
        return state.WriteLock ? default : stamp;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp.
    /// </summary>
    /// <param name="stamp">A stamp to check.</param>
    /// <returns><see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp; else <see langword="false"/>.</returns>
    public bool Validate(in LockStamp stamp) => stamp.IsValid(state);

    /// <summary>
    /// Attempts to obtain reader lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TryEnterReadLock()
    {
        ThrowIfDisposed();

        var manager = new ReadLockManager(state);
        return TryAcquire(ref manager);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory TryAcquireAsync(ref ReadLockManager manager, TimeSpan timeout, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new ReadLockManager(state);
        return TryAcquireAsync(ref manager, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref ReadLockManager manager, TimeSpan timeout, CancellationToken token = default)
        => WaitWithTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new ReadLockManager(state);
        return AcquireAsync(ref manager, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref ReadLockManager manager, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, token);

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
    {
        var manager = new ReadLockManager(state);
        return AcquireAsync(ref manager, token).Create();
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
        if (stamp.IsValid(state))
        {
            var manager = new WriteLockManager(state);
            return TryAcquire(ref manager);
        }

        return false;
    }

    /// <summary>
    /// Attempts to obtain writer lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TryEnterWriteLock()
    {
        ThrowIfDisposed();

        var manager = new WriteLockManager(state);
        return TryAcquire(ref manager);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory TryAcquireAsync(ref WriteLockManager manager, TimeSpan timeout, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new WriteLockManager(state);
        return TryAcquireAsync(ref manager, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref WriteLockManager manager, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, token);

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
    {
        var manager = new WriteLockManager(state);
        return AcquireAsync(ref manager, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref WriteLockManager manager, TimeSpan timeout, CancellationToken token)
        => WaitWithTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new WriteLockManager(state);
        return AcquireAsync(ref manager, timeout, token).Create();
    }

    /// <summary>
    /// Tries to upgrade the read lock to the write lock synchronously without blocking of the caller.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfuly; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool TryUpgradeToWriteLock()
    {
        ThrowIfDisposed();

        var manager = new UpgradeManager(state);
        return TryAcquire(ref manager);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory TryAcquireAsync(ref UpgradeManager manager, TimeSpan timeout, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new UpgradeManager(state);
        return TryAcquireAsync(ref manager, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref UpgradeManager manager, CancellationToken token)
        => WaitNoTimeout(ref manager, ref pool, token);

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
    {
        var manager = new UpgradeManager(state);
        return AcquireAsync(ref manager, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory AcquireAsync(ref UpgradeManager manager, TimeSpan timeout, CancellationToken token)
        => WaitWithTimeout(ref manager, ref pool, timeout, token);

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
    {
        var manager = new UpgradeManager(state);
        return AcquireAsync(ref manager, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private BooleanValueTaskFactory TryStealAsync(ref WriteLockManager manager, object? reason, TimeSpan timeout, CancellationToken token)
    {
        Interrupt(reason);
        return WaitNoTimeout(ref manager, ref pool, timeout, token);
    }

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
    {
        var manager = new WriteLockManager(state);
        return TryStealAsync(ref manager, reason, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory StealAsync(ref WriteLockManager manager, object? reason, TimeSpan timeout, CancellationToken token)
    {
        Interrupt(reason);
        return WaitWithTimeout(ref manager, ref pool, timeout, token);
    }

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
    {
        var manager = new WriteLockManager(state);
        return StealAsync(ref manager, reason, timeout, token).Create();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory StealAsync(ref WriteLockManager manager, object? reason, CancellationToken token)
    {
        Interrupt(reason);
        return WaitNoTimeout(ref manager, ref pool, token);
    }

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
    {
        var manager = new WriteLockManager(state);
        return StealAsync(ref manager, reason, token).Create();
    }

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));
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
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Release()
    {
        ThrowIfDisposed();

        if (state.IsWriteLockAllowed)
            throw new SynchronizationLockException(ExceptionMessages.NotInLock);

        state.ExitLock();
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
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