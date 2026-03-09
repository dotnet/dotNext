namespace DotNext.Threading;

partial struct AsyncLock
{
    private static readonly UserDataSlot<AsyncReaderWriterLock> ReaderWriterLock = new();
    private static readonly UserDataSlot<AsyncExclusiveLock> ExclusiveLock = new();

    private static AsyncReaderWriterLock GetReaderWriterLock(object obj)
    {
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case AsyncReaderWriterLock rwl:
                return rwl;
            case AsyncSharedLock or ReaderWriterLockSlim or AsyncExclusiveLock or SemaphoreSlim
                or WaitHandle or System.Threading.ReaderWriterLock:
                goto default;
            case not null when GC.GetGeneration(obj) is not int.MaxValue:
                return obj.UserData.GetOrSet(ReaderWriterLock);
            default:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
        }
    }

    private static AsyncLock GetExclusiveLock(object obj)
    {
        AsyncLock @lock;
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case AsyncSharedLock shared:
                @lock = Exclusive(shared);
                break;
            case AsyncExclusiveLock exclusive:
                @lock = Exclusive(exclusive);
                break;
            case SemaphoreSlim semaphore:
                @lock = Semaphore(semaphore);
                break;
            case AsyncReaderWriterLock rwl:
                @lock = WriteLock(rwl);
                break;
            case ReaderWriterLockSlim or WaitHandle or System.Threading.ReaderWriterLock:
                goto default;
            case not null when GC.GetGeneration(obj) is not int.MaxValue:
                @lock = Exclusive(obj.UserData.GetOrSet(ExclusiveLock));
                break;
            default:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
        }

        return @lock;
    }

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireLockAsync(object obj, CancellationToken token = default)
        => GetExclusiveLock(obj).AcquireAsync(token);

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static ValueTask<Scope> AcquireLockAsync(object obj, TimeSpan timeout, CancellationToken token = default)
        => GetExclusiveLock(obj).AcquireAsync(timeout, token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireReadLockAsync(object obj, CancellationToken token = default)
        => AsyncLock.ReadLock(GetReaderWriterLock(obj)).AcquireAsync(token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireReadLockAsync(object obj, TimeSpan timeout, CancellationToken token = default)
        => AsyncLock.ReadLock(GetReaderWriterLock(obj)).AcquireAsync(timeout, token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireWriteLockAsync(object obj, CancellationToken token = default)
        => AcquireWriteLockAsync(obj, upgrade: false, token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireWriteLockAsync(object obj, TimeSpan timeout, CancellationToken token = default)
        => AcquireWriteLockAsync(obj, upgrade: false, timeout, token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="upgrade">
    /// <see langword="true"/> to upgrade from read lock;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireWriteLockAsync(object obj, bool upgrade, CancellationToken token = default)
        => WriteLock(GetReaderWriterLock(obj), upgrade).AcquireAsync(token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="upgrade">
    /// <see langword="true"/> to upgrade from read lock;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<Scope> AcquireWriteLockAsync(object obj, bool upgrade, TimeSpan timeout, CancellationToken token = default)
        => WriteLock(GetReaderWriterLock(obj), upgrade).AcquireAsync(timeout, token);
}