using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Provides a set of methods to acquire different types of asynchronous lock.
/// </summary>
public static class AsyncLockAcquisition
{
    private static readonly UserDataSlot<AsyncReaderWriterLock> ReaderWriterLock = new();

    private static readonly UserDataSlot<AsyncExclusiveLock> ExclusiveLock = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AsyncReaderWriterLock GetReaderWriterLock<T>(this T obj)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is AsyncReaderWriterLock rwl)
            return rwl;

        if (GC.GetGeneration(obj) is int.MaxValue || obj is AsyncSharedLock or ReaderWriterLockSlim or AsyncExclusiveLock or SemaphoreSlim or WaitHandle or System.Threading.ReaderWriterLock)
            throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);

        return obj.GetUserData().GetOrSet(ReaderWriterLock);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AsyncLock GetExclusiveLock<T>(this T obj)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        AsyncLock @lock;
        if (obj is AsyncSharedLock shared)
        {
            @lock = AsyncLock.Exclusive(shared);
        }
        else if (obj is AsyncExclusiveLock exclusive)
        {
            @lock = AsyncLock.Exclusive(exclusive);
        }
        else if (obj is SemaphoreSlim semaphore)
        {
            @lock = AsyncLock.Semaphore(semaphore);
        }
        else if (obj is AsyncReaderWriterLock rwl)
        {
            @lock = AsyncLock.WriteLock(rwl);
        }
        else if (GC.GetGeneration(obj) is int.MaxValue || obj is ReaderWriterLockSlim or WaitHandle or System.Threading.ReaderWriterLock)
        {
            throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
        }
        else
        {
            @lock = AsyncLock.Exclusive(obj.GetUserData().GetOrSet(ExclusiveLock));
        }

        return @lock;
    }

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    [Obsolete("Use AcquireLockAsync(T, TimeSpan, CancellationToken) overload instead.", error: true)]
    public static ValueTask<AsyncLock.Holder> AcquireLockAsync<T>(T obj, TimeSpan timeout)
        where T : class => AcquireLockAsync(obj, timeout, CancellationToken.None);

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, CancellationToken token = default)
        where T : class => obj.GetExclusiveLock().AcquireAsync(token);

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, TimeSpan timeout, CancellationToken token = default)
        where T : class => obj.GetExclusiveLock().AcquireAsync(timeout, token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    [Obsolete("Use AcquireReadLockAsync(T, TimeSpan, CancellationToken) overload instead.", error: true)]
    public static ValueTask<AsyncLock.Holder> AcquireReadLockAsync<T>(T obj, TimeSpan timeout)
        where T : class => AcquireReadLockAsync(obj, timeout, CancellationToken.None);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, CancellationToken token = default)
        where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock()).AcquireAsync(token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, TimeSpan timeout, CancellationToken token = default)
        where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock()).AcquireAsync(timeout, token);

    /// <summary>
    /// Acquires writer lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    [Obsolete("Use AcquireWriteLockAsync(T, TimeSpan, CancellationToken) overload instead.", error: true)]
    public static ValueTask<AsyncLock.Holder> AcquireWriteLockAsync<T>(T obj, TimeSpan timeout)
        where T : class => AcquireWriteLockAsync(obj, timeout, CancellationToken.None);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, CancellationToken token = default)
        where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, TimeSpan timeout, CancellationToken token = default)
        where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(timeout, token);
}