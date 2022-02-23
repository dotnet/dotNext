using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Provides a set of methods to acquire different types of asynchronous lock.
/// </summary>
public static class AsyncLockAcquisition
{
    [SuppressMessage("Performance", "CA1805", Justification = "https://github.com/dotnet/roslyn-analyzers/issues/5750")]
    private static readonly UserDataSlot<AsyncReaderWriterLock> ReaderWriterLock = new();

    [SuppressMessage("Performance", "CA1805", Justification = "https://github.com/dotnet/roslyn-analyzers/issues/5750")]
    private static readonly UserDataSlot<AsyncExclusiveLock> ExclusiveLock = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AsyncReaderWriterLock GetReaderWriterLock<T>(this T obj)
        where T : class
    {
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case AsyncReaderWriterLock rwl:
                return rwl;
            case AsyncSharedLock or ReaderWriterLockSlim or AsyncExclusiveLock or SemaphoreSlim or WaitHandle or System.Threading.ReaderWriterLock:
            case string str when string.IsInterned(str) is not null:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
            default:
                return obj.GetUserData().GetOrSet(ReaderWriterLock);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AsyncLock GetExclusiveLock<T>(this T obj)
        where T : class
    {
        AsyncLock @lock;
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case AsyncSharedLock shared:
                @lock = AsyncLock.Exclusive(shared);
                break;
            case AsyncExclusiveLock exclusive:
                @lock = AsyncLock.Exclusive(exclusive);
                break;
            case SemaphoreSlim semaphore:
                @lock = AsyncLock.Semaphore(semaphore);
                break;
            case AsyncReaderWriterLock rwl:
                @lock = AsyncLock.WriteLock(rwl);
                break;
            case ReaderWriterLockSlim or WaitHandle or System.Threading.ReaderWriterLock:
            case string str when string.IsInterned(str) is not null:
                throw new InvalidOperationException(ExceptionMessages.UnsupportedLockAcquisition);
            default:
                @lock = AsyncLock.Exclusive(obj.GetUserData().GetOrSet(ExclusiveLock));
                break;
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
    public static ValueTask<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, TimeSpan timeout)
        where T : class => obj.GetExclusiveLock().AcquireAsync(timeout);

    /// <summary>
    /// Acquires exclusive lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    public static ValueTask<AsyncLock.Holder> AcquireLockAsync<T>(this T obj, CancellationToken token)
        where T : class => obj.GetExclusiveLock().AcquireAsync(token);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, TimeSpan timeout)
        where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock()).AcquireAsync(timeout);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    public static ValueTask<AsyncLock.Holder> AcquireReadLockAsync<T>(this T obj, CancellationToken token)
        where T : class => AsyncLock.ReadLock(obj.GetReaderWriterLock()).AcquireAsync(token);

    /// <summary>
    /// Acquires writer lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <returns>The acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    public static ValueTask<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, TimeSpan timeout)
        where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(timeout);

    /// <summary>
    /// Acquires reader lock associated with the given object.
    /// </summary>
    /// <typeparam name="T">The type of the object to be locked.</typeparam>
    /// <param name="obj">The object to be locked.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The acquired lock holder.</returns>
    public static ValueTask<AsyncLock.Holder> AcquireWriteLockAsync<T>(this T obj, CancellationToken token)
        where T : class => AsyncLock.WriteLock(obj.GetReaderWriterLock()).AcquireAsync(token);

    /// <summary>
    /// Suspends <see cref="ObjectDisposedException"/> if the target lock
    /// has been disposed.
    /// </summary>
    /// <remarks>
    /// This method is usually combined with <see cref="AsyncLock.TryAcquireAsync(CancellationToken)"/> or
    /// <see cref="AsyncLock.TryAcquireAsync(TimeSpan, CancellationToken)"/> calls
    /// to avoid <see cref="ObjectDisposedException"/> if the lock is already disposed
    /// at the time of the call. If the lock is disposed then this method returns empty <see cref="AsyncLock.Holder"/>.
    /// </remarks>
    /// <param name="result">The result of the lock acquisition.</param>
    /// <returns>The task representing the lock acquisition.</returns>
    public static async ValueTask<AsyncLock.Holder> SuppressDisposedState(this ValueTask<AsyncLock.Holder> result)
    {
        try
        {
            return await result.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return default;
        }
    }

    /// <summary>
    /// Suspends cancellation of lock acquisition and converts the canceled operation result
    /// into unsuccessfully acquired lock.
    /// </summary>
    /// <remarks>
    /// This method is usually combined with <see cref="AsyncLock.TryAcquireAsync(CancellationToken)"/> or
    /// <see cref="AsyncLock.TryAcquireAsync(TimeSpan, CancellationToken)"/> calls
    /// to avoid <see cref="OperationCanceledException"/> if the lock acquisition is already canceled
    /// at the time of the call. If the acqusition is canceled then this method returns empty <see cref="AsyncLock.Holder"/>.
    /// </remarks>
    /// <param name="result">The result of the lock acquisition.</param>
    /// <returns>The task representing the lock acquisition.</returns>
    public static async ValueTask<AsyncLock.Holder> SuppressCancellation(this ValueTask<AsyncLock.Holder> result)
    {
        try
        {
            return await result.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }

    /// <summary>
    /// Suspends cancellation of lock acquisition or <see cref="ObjectDisposedException"/> if the target lock
    /// has been disposed.
    /// </summary>
    /// <remarks>
    /// This method is usually combined with <see cref="AsyncLock.TryAcquireAsync(CancellationToken)"/> or
    /// <see cref="AsyncLock.TryAcquireAsync(TimeSpan, CancellationToken)"/> calls
    /// to replace <see cref="OperationCanceledException"/> or <see cref="OperationCanceledException"/>
    /// with empty <see cref="AsyncLock.Holder"/>.
    /// </remarks>
    /// <param name="result">The result of the lock acquisition.</param>
    /// <returns>The task representing the lock acquisition.</returns>
    public static async ValueTask<AsyncLock.Holder> SuppressDisposedStateOrCancellation(this ValueTask<AsyncLock.Holder> result)
    {
        try
        {
            return await result.ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            return default;
        }
    }
}