using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    private new sealed class WaitNode :
        QueuedSynchronizer.WaitNode,
        INodeMapper<WaitNode, bool>
    {
        internal bool IsWriteLock;

        static bool INodeMapper<WaitNode, bool>.GetValue(WaitNode node)
            => node.IsWriteLock;
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

        internal readonly long ReadLocks => Volatile.Read(in readLocks);

        internal readonly ulong Version => Volatile.Read(in version);

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
    private struct ReadLockManager : ILockManager, IConsumer<WaitNode>
    {
        private State state;

        readonly bool ILockManager.IsLockAllowed
            => state.IsReadLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireReadLock();

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node)
        {
            node.IsWriteLock = false;
            node.DrainOnReturn = true;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct WriteLockManager : ILockManager, IConsumer<WaitNode>
    {
        private State state;

        readonly bool ILockManager.IsLockAllowed
            => state.IsWriteLockAllowed;

        void ILockManager.AcquireLock()
            => state.AcquireWriteLock();

        readonly void IConsumer<WaitNode>.Invoke(WaitNode node)
            => Initialize(node);

        public static void Initialize(WaitNode node)
            => node.IsWriteLock = node.DrainOnReturn = true;
    }

    private State state;
    private ThreadLocal<bool>? lockOwnerState;

    /// <summary>
    /// Initializes a new reader/writer lock.
    /// </summary>
    public AsyncReaderWriterLock() => state = new();
    
    private bool IsLockHelpByCurrentThread
    {
        get => lockOwnerState?.Value ?? false;
        set
        {
            if (lockOwnerState is { } ownerFlag)
            {
                ownerFlag.Value = value;
            }
            else if (value)
            {
                Monitor.Enter(SyncRoot);
                ownerFlag = lockOwnerState ??= new(trackAllValues: false);
                Monitor.Exit(SyncRoot);

                ownerFlag.Value = true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TLockManager GetLockManager<TLockManager>()
        where TLockManager : struct, ILockManager, IConsumer<WaitNode>
        => ref Unsafe.As<State, TLockManager>(ref state);

    private bool Signal(ref WaitQueueVisitor waitQueueVisitor, bool isWriteLock) => isWriteLock
        ? waitQueueVisitor.SignalCurrent(ref GetLockManager<WriteLockManager>())
        : waitQueueVisitor.SignalCurrent(ref GetLockManager<ReadLockManager>());

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
    {
        while (!waitQueueVisitor.IsEndOfQueue<WaitNode, bool>(out var isWriteLock) && Signal(ref waitQueueVisitor, isWriteLock))
        {
            waitQueueVisitor.Advance();
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
    public bool Validate(in LockStamp stamp) => stamp.IsValid(in state);

    /// <summary>
    /// Tries to obtain reader lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterReadLock()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryEnter<ReadLockManager>();
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
        => TryEnter<ReadLockManager>(timeout, token);

    private bool TryEnter<TLockManager>(TimeSpan timeout, CancellationToken token)
        where TLockManager : struct, ILockManager, IConsumer<WaitNode>
    {
        if (IsLockHelpByCurrentThread)
            throw new LockRecursionException();

        bool result;
        try
        {
            IsLockHelpByCurrentThread =
                result = TryAcquireAsync<WaitNode, TLockManager>(ref GetLockManager<TLockManager>(), timeout, token)
                    .Wait();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            result = false;
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
        => TryAcquireAsync<WaitNode, ReadLockManager>(ref GetLockManager<ReadLockManager>(), timeout, token);

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
        => AcquireAsync<WaitNode, ReadLockManager>(ref GetLockManager<ReadLockManager>(), timeout, token);

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
        => AcquireAsync<WaitNode, ReadLockManager>(ref GetLockManager<ReadLockManager>(), token);

    /// <summary>
    /// Attempts to acquire write lock without blocking.
    /// </summary>
    /// <param name="stamp">The stamp of the read lock.</param>
    /// <returns><see langword="true"/> if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock(in LockStamp stamp)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Monitor.Enter(SyncRoot);
        var result = stamp.IsValid(in state) && TryAcquire(ref GetLockManager<WriteLockManager>());
        Monitor.Exit(SyncRoot);

        if (result)
        {
            IsLockHelpByCurrentThread = true;
        }

        return result;
    }

    /// <summary>
    /// Attempts to obtain writer lock synchronously without blocking caller thread.
    /// </summary>
    /// <returns><see langword="true"/> if lock is taken successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryEnterWriteLock()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TryEnter<WriteLockManager>();
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
        return TryEnter<WriteLockManager>(timeout, token);
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
        => TryAcquireAsync<WaitNode, WriteLockManager>(ref GetLockManager<WriteLockManager>(), timeout, token);

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
        => AcquireAsync<WaitNode, WriteLockManager>(ref GetLockManager<WriteLockManager>(), token);

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
        => AcquireAsync<WaitNode, WriteLockManager>(ref GetLockManager<WriteLockManager>(), timeout, token);

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

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        bool result;
        lock (SyncRoot)
        {
            if (state.HasNoReadLocks)
                throw new SynchronizationLockException();

            result = TryUpgradeToWriteLock(out suspendedCallers);
        }

        suspendedCallers?.Unwind();
        if (result)
        {
            IsLockHelpByCurrentThread = true;
        }

        return result;
    }

    private bool TryEnter<TLockManager>()
        where TLockManager : struct, ILockManager, IConsumer<WaitNode>
    {
        Monitor.Enter(SyncRoot);
        var result = TryAcquire(ref GetLockManager<TLockManager>());
        Monitor.Exit(SyncRoot);

        if (result)
        {
            IsLockHelpByCurrentThread = true;
        }
        
        return result;
    }

    private bool TryUpgradeToWriteLock(out LinkedValueTaskCompletionSource<bool>? suspendedCallers)
    {
        Debug.Assert(state.ReadLocks > 0L);

        state.ExitReadLock();

        suspendedCallers = DrainWaitQueue();
        return TryAcquire(ref GetLockManager<WriteLockManager>());
    }

    private T UpgradeToWriteLockAsync<T, TBuilder>(ref TBuilder builder)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>
    {
        var suspendedCallers = default(LinkedValueTaskCompletionSource<bool>);
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when state.HasNoReadLocks:
                builder.Dispose();
                var task = TBuilder.FromException(new SynchronizationLockException());
                goto exit;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, TryUpgradeToWriteLock(out suspendedCallers)) is { } node:
                WriteLockManager.Initialize(node);
                goto default;
            default:
                builder.Dispose();
                suspendedCallers?.Unwind();
                task = builder.Invoke();
                exit:
                return task;
        }
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
        var builder = CreateTaskBuilder(timeout, token);
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
        var builder = CreateTaskBuilder(token);
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
        var builder = CreateTaskBuilder(timeout, token);
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
        => TryAcquireAsync<WaitNode, WriteLockManager>(reason, ref GetLockManager<WriteLockManager>(), timeout, token);

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
        => AcquireAsync<WaitNode, WriteLockManager>(reason, ref GetLockManager<WriteLockManager>(), timeout, token);

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
        => AcquireAsync<WaitNode, WriteLockManager>(reason, ref GetLockManager<WriteLockManager>(), token);

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

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.ExitLock();
            IsLockHelpByCurrentThread = false;
            suspendedCallers = DrainWaitQueue();

            if (IsDisposing && IsReadyToDispose)
            {
                Dispose(true);
            }
        }

        suspendedCallers?.Unwind();
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

        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            if (state.IsWriteLockAllowed)
                throw new SynchronizationLockException(ExceptionMessages.NotInLock);

            state.DowngradeFromWriteLock();
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
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
}