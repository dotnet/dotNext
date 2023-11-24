using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Provides a set of methods to acquire different types of lock.
/// </summary>
public static class LockAcquisition
{
    private static readonly UserDataSlot<ReaderWriterLockSlim> ReaderWriterLock = new();

    private sealed class ReaderWriterLockSlimWithRecursion : ReaderWriterLockSlim
    {
        public ReaderWriterLockSlimWithRecursion()
            : base(LockRecursionPolicy.SupportsRecursion)
        {
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReaderWriterLockSlim GetReaderWriterLock<T>(this T obj)
        where T : class
    {
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case ReaderWriterLockSlim rws:
                return rws;
            case SemaphoreSlim or WaitHandle or System.Threading.ReaderWriterLock:
            case string str when string.IsInterned(str) is not null:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
            default:
                return obj.GetUserData().GetOrSet<ReaderWriterLockSlim, ReaderWriterLockSlimWithRecursion>(ReaderWriterLock);
        }
    }

    /// <summary>
    /// Acquires read lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired read lock.</returns>
    public static Lock.Holder AcquireReadLock<T>(this T obj)
        where T : class
        =>
        Lock.ReadLock(obj.GetReaderWriterLock(), false).Acquire();

    /// <summary>
    /// Acquires read lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired read lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Lock.Holder AcquireReadLock<T>(this T obj, TimeSpan timeout)
        where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), false).Acquire(timeout);

    /// <summary>
    /// Acquires write lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired write lock.</returns>
    public static Lock.Holder AcquireWriteLock<T>(this T obj)
        where T : class => Lock.WriteLock(obj.GetReaderWriterLock()).Acquire();

    /// <summary>
    /// Acquires write lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired write lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Lock.Holder AcquireWriteLock<T>(this T obj, TimeSpan timeout)
        where T : class => Lock.WriteLock(obj.GetReaderWriterLock()).Acquire(timeout);

    /// <summary>
    /// Acquires upgradeable read lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <returns>The acquired upgradeable read lock.</returns>
    public static Lock.Holder AcquireUpgradeableReadLock<T>(this T obj)
        where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), true).Acquire();

    /// <summary>
    /// Acquires upgradeable read lock for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The amount of time to wait for the lock.</param>
    /// <returns>The acquired upgradeable read lock.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static Lock.Holder AcquireUpgradeableReadLock<T>(this T obj, TimeSpan timeout)
        where T : class => Lock.ReadLock(obj.GetReaderWriterLock(), true).Acquire(timeout);
}