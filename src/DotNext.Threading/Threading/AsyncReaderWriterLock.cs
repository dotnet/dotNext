using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks;

/// <summary>
/// Represents asynchronous version of <see cref="ReaderWriterLockSlim"/>.
/// </summary>
/// <remarks>
/// This lock doesn't support recursion.
/// </remarks>
[DebuggerDisplay($"Readers = {{{nameof(CurrentReadCount)}}}, WriteLockHeld = {{{nameof(IsWriteLockHeld)}}}")]
public partial class AsyncReaderWriterLock : QueuedSynchronizer, IAsyncDisposable
{
    private State state;
    private ThreadLocal<bool>? lockOwnerState;

    /// <summary>
    /// Initializes a new reader/writer lock.
    /// </summary>
    public AsyncReaderWriterLock() => state = new();

    private bool IsLockHelpByCurrentThread => lockOwnerState?.Value ?? false;

    private void InitializeLockOwner(bool acquired)
    {
        if (lockOwnerState is null && acquired)
        {
            lockOwnerState = new(trackAllValues: false);
        }
    }

    private bool Signal(ref WaitQueueScope queue, bool isWriteLock) => isWriteLock
        ? queue.SignalCurrent<WriteLockManager>(new(ref state))
        : queue.SignalCurrent<ReadLockManager>(new(ref state));

    private protected sealed override void DrainWaitQueue(ref WaitQueueScope queue)
    {
        while (!queue.IsEndOfQueue<WaitNode, bool>(out var isWriteLock) && Signal(ref queue, isWriteLock))
        {
            queue.Advance();
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Ordering of version and lock state must be respected:
        // Write lock acquisition changes the state to Acquired and then increments the version.
        // Optimistic read lock reads the version and then checks Acquired lock state to avoid false positives.
        var stamp = new LockStamp(in state);
        return state.WriteLock ? default : stamp;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp.
    /// </summary>
    /// <param name="stamp">A stamp to check.</param>
    /// <returns><see langword="true"/> if the lock has not been exclusively acquired since issuance of the given stamp; else <see langword="false"/>.</returns>
    public bool Validate(in LockStamp stamp) => stamp.IsInitialized && state.IsValidVersion(stamp.Version);

    /// <summary>
    /// Tries to obtain reader lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterReadLock()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryEnter<ReadLockManager>(new(ref state));
    }

    /// <summary>
    /// Tries to obtain reader lock synchronously.
    /// </summary>
    /// <param name="timeout">The time to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if reader lock is acquired in timely manner; <see langword="false"/> if timed out or canceled.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="LockRecursionException">The lock is already acquired by the current thread.</exception>
    public bool TryEnterReadLock(TimeSpan timeout, CancellationToken token = default)
        => TryEnter<ReadLockManager>(new(ref state), timeout, token);

    private bool TryEnter<TLockManager>(TLockManager manager, TimeSpan timeout, CancellationToken token)
        where TLockManager : struct, ILockManager<WaitNode>, allows ref struct
    {
        if (IsLockHelpByCurrentThread)
            throw new LockRecursionException();

        bool result;
        try
        {
            var builder = BeginAcquisition(timeout, token);
            result = EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, TLockManager>(ref builder, manager)
                .Wait();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            result = false;
        }

        if (result && lockOwnerState is null)
        {
            TryAcquire(new LockOwnerTransition(ref lockOwnerState), out _).Dispose();

            Debug.Assert(lockOwnerState is not null);
            lockOwnerState.Value = true;
        }

        return result;
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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryEnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, ReadLockManager>(ref builder, new(ref state));
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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask EnterReadLockAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, ReadLockManager>(ref builder, new(ref state));
    }

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
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, ReadLockManager>(ref builder, new(ref state));
    }

    /// <summary>
    /// Attempts to acquire write lock without blocking.
    /// </summary>
    /// <param name="stamp">The stamp of the read lock.</param>
    /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock(in LockStamp stamp)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool acquired;
        if (stamp.IsInitialized)
        {
            TryAcquire(new ProtectedWriteLockManager(ref state, stamp.Version), out acquired).Dispose();
        }
        else
        {
            acquired = false;
        }

        lockOwnerState?.Value = acquired;

        return acquired;
    }

    /// <summary>
    /// Attempts to obtain writer lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryEnter<WriteLockManager>(new(ref state));
    }

    /// <summary>
    /// Tries to obtain writer lock synchronously.
    /// </summary>
    /// <param name="timeout">The time to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if writer lock is acquired in timely manner; <see langword="false"/> if timed out or canceled.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="LockRecursionException">The lock is already acquired by the current thread.</exception>
    public bool TryEnterWriteLock(TimeSpan timeout, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        return TryEnter<WriteLockManager>(new(ref state), timeout, token);
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
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    public ValueTask<bool> TryEnterWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, WriteLockManager>(ref builder, new(ref state));
    }

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
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, WriteLockManager>(ref builder, new(ref state));
    }

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
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, WriteLockManager>(ref builder, new(ref state));
    }

    /// <summary>
    /// Tries to upgrade the read lock to the write lock synchronously without blocking of the caller.
    /// </summary>
    /// <remarks>
    /// When this method is called, the reader lock is released, and the caller goes to the end of the queue for the writer lock.
    /// Thus, other callers might write to the resource before the caller that requested the upgrade is granted the writer lock.
    /// If this method returns <see langword="false"/>, it doesn't reacquire the reader lock.
    /// </remarks>
    /// <example>
    /// See example for <see cref="UpgradeToWriteLockAsync(CancellationToken)"/>
    /// </example>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="SynchronizationLockException">The caller wasn't acquire the reader lock.</exception>
    public bool TryUpgradeToWriteLock()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool upgraded;
        var queue = CaptureWaitQueue();
        try
        {
            if (state.HasNoReadLocks)
                throw new SynchronizationLockException();

            upgraded = TryUpgradeToWriteLock(ref queue);
            InitializeLockOwner(upgraded);
        }
        finally
        {
            queue.Dispose();
        }

        lockOwnerState?.Value = upgraded;

        return upgraded;
    }

    private bool TryEnter<TLockManager>(TLockManager manager)
        where TLockManager : struct, ILockManager, allows ref struct
    {
        bool acquired;
        using (TryAcquire(manager, out acquired))
        {
            InitializeLockOwner(acquired);
        }

        lockOwnerState?.Value = acquired;

        return acquired;
    }

    private bool TryUpgradeToWriteLock(ref WaitQueueScope queue)
    {
        Debug.Assert(state.ReadLocks > 0L);

        state.ExitReadLock();

        DrainWaitQueue(ref queue);
        var acquired = state.IsWriteLockAllowed;
        if (acquired)
        {
            state.AcquireWriteLock();
        }

        return acquired;
    }

    private T UpgradeToWriteLockAsync<T, TBuilder>(ref TBuilder builder)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, IWaitQueueProvider, allows ref struct
    {
        WaitQueueScope queue;
        if (builder.IsCompleted)
        {
            queue = default;
        }
        else if (state.HasNoReadLocks)
        {
            queue = default;
            builder.Complete<DefaultExceptionFactory<SynchronizationLockException>>();
        }
        else
        {
            queue = builder.CaptureWaitQueue();
            if (Acquire<T, TBuilder, WaitNode>(ref builder, TryUpgradeToWriteLock(ref queue)) is { } node)
                WriteLockManager.Initialize(node);
        }

        var task = builder.Build();
        queue.ResumeSuspendedCallers();
        return task;
    }

    /// <summary>
    /// Tries to upgrade the read lock to the write lock asynchronously.
    /// </summary>
    /// <remarks>
    /// When this method is called, the reader lock is released, and the caller goes to the end of the queue for the writer lock.
    /// Thus, other callers might write to the resource before the caller that requested the upgrade is granted the writer lock.
    /// If this method fails or returns <see langword="false"/>, it doesn't reacquire the reader lock.
    /// </remarks>
    /// <example>
    /// See example for <see cref="UpgradeToWriteLockAsync(CancellationToken)"/>
    /// </example>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered upgradeable mode; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    /// <exception cref="SynchronizationLockException">The caller wasn't acquire the reader lock.</exception>
    public ValueTask<bool> TryUpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return UpgradeToWriteLockAsync<ValueTask<bool>, TimeoutAndCancellationToken>(ref builder);
    }

    /// <summary>
    /// Upgrades the read lock to the write lock asynchronously.
    /// </summary>
    /// <remarks>
    /// When this method is called, the reader lock is released, and the caller goes to the end of the queue for the writer lock.
    /// Thus, other callers might write to the resource before the caller that requested the upgrade is granted the writer lock.
    /// If this method fails, it doesn't reacquire the reader lock.
    /// </remarks>
    /// <example>
    /// The recommended pattern for upgrade is the following:
    /// <code>
    /// AsyncReaderWriterLock rwl;
    /// var lockTaken = false;
    /// await rwl.EnterReadLockAsync(token);
    /// lockTaken = true;
    /// try
    /// {
    ///   // do read-only stuff ...
    ///   // now upgrade the lock
    ///   lockTaken = false;
    ///   await rwl.UpgradeToWriteLockAsync(token);
    ///   lockTaken = true;
    ///   try
    ///   {
    ///     // do write
    ///   }
    ///   finally
    ///   {
    ///     rwl.DowngradeFromWriteLock();
    ///   }
    /// }
    /// finally
    /// {
    ///   if (lockTaken)
    ///     rwl.Release();
    /// }
    /// </code>
    /// <c>lockTaken</c> is needed here because <see cref="UpgradeToWriteLockAsync(CancellationToken)"/> could throw, but
    /// it always releases the reader lock. In that case, <c>finally</c> block should not release the reader lock twice.
    /// </example>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    /// <exception cref="SynchronizationLockException">The caller wasn't acquire the reader lock.</exception>
    public ValueTask UpgradeToWriteLockAsync(CancellationToken token = default)
    {
        var builder = BeginAcquisition(token);
        return UpgradeToWriteLockAsync<ValueTask, CancellationTokenOnly>(ref builder);
    }

    /// <summary>
    /// Upgrades the read lock to the write lock asynchronously.
    /// </summary>
    /// <remarks>
    /// When this method is called, the reader lock is released, and the caller goes to the end of the queue for the writer lock.
    /// Thus, other callers might write to the resource before the caller that requested the upgrade is granted the writer lock.
    /// If this method fails, it doesn't reacquire the reader lock.
    /// </remarks>
    /// <example>
    /// See example for <see cref="UpgradeToWriteLockAsync(CancellationToken)"/>
    /// </example>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="PendingTaskInterruptedException">The operation has been interrupted manually.</exception>
    /// <exception cref="SynchronizationLockException">The caller wasn't acquire the reader lock.</exception>
    public ValueTask UpgradeToWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var builder = BeginAcquisition(timeout, token);
        return UpgradeToWriteLockAsync<ValueTask, TimeoutAndCancellationToken>(ref builder);
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
        var e = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask<bool>, TimeoutAndCancellationToken, WaitNode, WriteLockManager>(e, ref builder, new(ref state));
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
        var e = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(timeout, token);
        return EndAcquisition<ValueTask, TimeoutAndCancellationToken, WaitNode, WriteLockManager>(e, ref builder, new(ref state));
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
        var e = PendingTaskInterruptedException.CreateAndFillStackTrace(reason);
        var builder = BeginAcquisition(token);
        return EndAcquisition<ValueTask, CancellationTokenOnly, WaitNode, WriteLockManager>(e, ref builder, new(ref state));
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            lockOwnerState?.Value = false;
            DrainWaitQueue(ref queue);

            if (IsDisposing && IsReadyToDispose)
            {
                Dispose(true);
            }
        }
        finally
        {
            queue.Dispose();
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var queue = CaptureWaitQueue();
        try
        {
            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.DowngradeFromWriteLock();
            DrainWaitQueue(ref queue);
        }
        finally
        {
            queue.Dispose();
        }
    }

    private protected sealed override bool IsReadyToDispose => state.IsWriteLockAllowed && IsEmptyQueue;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lockOwnerState?.Dispose();
        }

        base.Dispose(disposing);
    }
    
    private new sealed class WaitNode :
        QueuedSynchronizer.WaitNode,
        IWaitNodeFeature<bool>
    {
        internal bool IsWriteLock;

        bool IWaitNodeFeature<bool>.Feature => IsWriteLock;
    }

    // describes internal state of reader/writer lock
    [StructLayout(LayoutKind.Auto)]
    [SuppressMessage("Usage", "CA1001", Justification = "The disposable field is disposed in the Dispose() method")]
    internal struct State
    {
        private ulong version;  // version of write lock

        // number of acquired read locks
        private long readLocks; // volatile
        private bool writeLock;

        internal readonly bool WriteLock => Volatile.Read(in writeLock);

        internal void DowngradeFromWriteLock()
        {
            writeLock = false;
            readLocks = 1L;
        }

        internal void ExitLock()
        {
            if (writeLock)
            {
                ExitWriteLock();
            }
            else
            {
                ExitReadLock();
            }
        }

        private void ExitWriteLock()
        {
            writeLock = false;
            readLocks = 0L;
        }

        internal void ExitReadLock() => readLocks--;

        internal readonly long ReadLocks => Atomic.Read(in readLocks);

        internal readonly ulong Version => Atomic.Read(in version);

        internal bool IsValidVersionUnsafe(ulong expected) => version == expected;

        internal bool IsValidVersion(ulong expected)
        {
            var currentVer = version;
            var writeLockAcquired = writeLock;
            Volatile.ReadBarrier();
            
            return currentVer == expected && !writeLockAcquired;
        }

        internal readonly bool IsWriteLockAllowed => writeLock is false && HasNoReadLocks;

        internal readonly bool HasNoReadLocks => readLocks is 0L;

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
    private readonly ref struct ReadLockManager(ref State state) : ILockManager<WaitNode>
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed => state.IsReadLockAllowed;

        void ILockManager.AcquireLock() => state.AcquireReadLock();

        static void ILockManager<WaitNode>.Initialize(WaitNode node)
            => node.IsWriteLock = false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ProtectedWriteLockManager(ref State state, ulong expectedVersion) : ILockManager
    {
        private readonly ref State state = ref state;

        public bool IsLockAllowed => state.IsValidVersionUnsafe(expectedVersion) && state.IsWriteLockAllowed;

        public void AcquireLock() => state.AcquireWriteLock();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct WriteLockManager(ref State state) : ILockManager<WaitNode>
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed
            => state.IsWriteLockAllowed;

        void ILockManager.AcquireLock() => state.AcquireWriteLock();

        public static void Initialize(WaitNode node)
            => node.IsWriteLock = true;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct LockOwnerTransition(ref ThreadLocal<bool>? tls) : ILockManager
    {
        private readonly ref ThreadLocal<bool>? tls = ref tls;
        
        bool ILockManager.IsLockAllowed => true;

        void ILockManager.AcquireLock() => tls ??= new(trackAllValues: false);
        
        static bool ILockManager.RequiresEmptyQueue => false;
    }
}