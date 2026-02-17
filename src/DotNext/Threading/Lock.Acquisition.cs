namespace DotNext.Threading;

partial struct Lock
{
    private static readonly UserDataSlot<ReaderWriterLockSlim> ReaderWriterLock = new();

    private sealed class ReaderWriterLockSlimWithRecursion() : ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

    private static ReaderWriterLockSlim GetReaderWriterLock(object obj)
    {
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case ReaderWriterLockSlim rws:
                return rws;
            case SemaphoreSlim or WaitHandle or System.Threading.ReaderWriterLock:
                goto default;
            case not null when GC.GetGeneration(obj) is not int.MaxValue:
                return obj.UserData.GetOrSet<ReaderWriterLockSlim, ReaderWriterLockSlimWithRecursion>(ReaderWriterLock);
            default:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
        }
    }

    /// <summary>
    /// Acquires read lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired read lock.</returns>
    public static Scope AcquireReadLock(object obj)
        => ReadLock(GetReaderWriterLock(obj), false).Acquire();

    /// <summary>
    /// Acquires read lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired read lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Scope AcquireReadLock(object obj, TimeSpan timeout)
        => ReadLock(GetReaderWriterLock(obj), false).Acquire(timeout);

    /// <summary>
    /// Acquires write lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired write lock.</returns>
    public static Scope AcquireWriteLock(object obj)
        => WriteLock(GetReaderWriterLock(obj)).Acquire();

    /// <summary>
    /// Acquires write lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired write lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Scope AcquireWriteLock(object obj, TimeSpan timeout)
        => WriteLock(GetReaderWriterLock(obj)).Acquire(timeout);

    /// <summary>
    /// Acquires upgradeable read lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired upgradeable read lock.</returns>
    public static Scope AcquireUpgradeableReadLock(object obj)
        => ReadLock(GetReaderWriterLock(obj), true).Acquire();

    /// <summary>
    /// Acquires upgradeable read lock for the specified object.
    /// </summary>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired upgradeable read lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Scope AcquireUpgradeableReadLock(object obj, TimeSpan timeout)
        => ReadLock(GetReaderWriterLock(obj), true).Acquire(timeout);
}