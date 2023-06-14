using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Threading.Timeout;

namespace DotNext.Threading;

/// <summary>
/// Unified representation of asynchronous exclusive lock, semaphore lock, read lock, write lock or upgradeable read lock.
/// </summary>
/// <remarks>
/// Lock acquisition is asynchronous operation. Note that non-blocking asynchronous locks are not intersected with
/// their blocking alternatives except semaphore. It means that exclusive lock obtained in blocking manner doesn't
/// prevent acquisition of asynchronous lock obtained in non-blocking manner.
/// </remarks>
/// <seealso cref="Lock"/>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"LockType = {{{nameof(LockTypeName)}}}, IsOwner = {{{nameof(owner)}}}")]
public struct AsyncLock : IDisposable, IEquatable<AsyncLock>, IAsyncDisposable
{
    internal enum Type : byte
    {
        None = 0,
        Exclusive,
        ReadLock,
        Upgrade,
        WriteLock,
        Semaphore,
        Weak,
        Strong,
    }

    /// <summary>
    /// Represents acquired asynchronous lock.
    /// </summary>
    /// <remarks>
    /// The lock can be released by calling <see cref="Dispose()"/>.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public struct Holder : IDisposable
    {
        private readonly object lockedObject;
        private readonly Type type;

        internal Holder(object lockedObject, Type type)
        {
            Debug.Assert(lockedObject is not null);
            Debug.Assert(type is not Type.None);

            this.lockedObject = lockedObject;
            this.type = type;
        }

        /// <summary>
        /// Gets a value indicating that this object doesn't hold the lock.
        /// </summary>
        public readonly bool IsEmpty => lockedObject is null;

        /// <summary>
        /// Releases the acquired lock.
        /// </summary>
        /// <remarks>
        /// This object is not reusable after calling of this method.
        /// </remarks>
        public void Dispose()
        {
            switch (type)
            {
                case Type.Exclusive:
                    As<AsyncExclusiveLock>(lockedObject).Release();
                    break;
                case Type.ReadLock or Type.WriteLock:
                    As<AsyncReaderWriterLock>(lockedObject).Release();
                    break;
                case Type.Upgrade:
                    As<AsyncReaderWriterLock>(lockedObject).DowngradeFromWriteLock();
                    break;
                case Type.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Release(1);
                    break;
                case Type.Strong or Type.Weak:
                    As<AsyncSharedLock>(lockedObject).Release();
                    break;
            }

            this = default;
        }

        /// <summary>
        /// Indicates that the object holds successfully acquired lock.
        /// </summary>
        /// <param name="holder">The lock holder.</param>
        /// <returns><see langword="true"/>, if the object holds successfully acquired lock; otherwise, <see langword="false"/>.</returns>
        public static implicit operator bool(in Holder holder) => holder.lockedObject is not null;
    }

    private readonly object lockedObject;
    private readonly Type type;
    private readonly bool owner;

    private AsyncLock(object lockedObject, Type type, bool owner)
    {
        Debug.Assert(lockedObject is not null);
        Debug.Assert(type is not Type.None);

        this.lockedObject = lockedObject;
        this.type = type;
        this.owner = owner;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private readonly string? LockTypeName => Enum.GetName(type);

    private readonly Holder CreateHolder() => new(lockedObject, type);

    /// <summary>
    /// Creates exclusive asynchronous lock but doesn't acquire it.
    /// </summary>
    /// <remarks>
    /// Constructed lock owns the exclusive lock instance.
    /// </remarks>
    /// <returns>Exclusive asynchronous lock.</returns>
    /// <seealso cref="AsyncExclusiveLock"/>
    public static AsyncLock Exclusive() => new(new AsyncExclusiveLock(), Type.Exclusive, true);

    /// <summary>
    /// Wraps exclusive lock into the unified representation of asynchronous lock.
    /// </summary>
    /// <param name="lock">The lock object to be wrapped.</param>
    /// <returns>Exclusive asynchronous lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    public static AsyncLock Exclusive(AsyncExclusiveLock @lock)
        => new(@lock ?? throw new ArgumentNullException(nameof(@lock)), Type.Exclusive, false);

    /// <summary>
    /// Wraps semaphore instance into the unified representation of the lock.
    /// </summary>
    /// <param name="semaphore">The semaphore to wrap into lock object.</param>
    /// <returns>The lock representing semaphore.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="semaphore"/> is <see langword="null"/>.</exception>
    public static AsyncLock Semaphore(SemaphoreSlim semaphore)
        => new(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), Type.Semaphore, false);

    /// <summary>
    /// Creates semaphore-based lock but doesn't acquire the lock.
    /// </summary>
    /// <remarks>
    /// Constructed lock owns the semaphore instance.
    /// </remarks>
    /// <param name="initialCount">The initial number of requests for the semaphore that can be granted concurrently.</param>
    /// <param name="maxCount">The maximum number of requests for the semaphore that can be granted concurrently.</param>
    /// <returns>The lock representing semaphore.</returns>
    public static AsyncLock Semaphore(int initialCount, int maxCount)
        => new(new SemaphoreSlim(initialCount, maxCount), Type.Semaphore, true);

    /// <summary>
    /// Creates read lock but doesn't acquire it.
    /// </summary>
    /// <param name="rwLock">Read/write lock source.</param>
    /// <returns>Reader lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rwLock"/> is <see langword="null"/>.</exception>
    public static AsyncLock ReadLock(AsyncReaderWriterLock rwLock)
        => new(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), Type.ReadLock, false);

    /// <summary>
    /// Creates write lock but doesn't acquire it.
    /// </summary>
    /// <param name="rwLock">Read/write lock source.</param>
    /// <param name="upgrade">
    /// <see langword="true"/> to upgrade from read lock;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>Write-only lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rwLock"/> is <see langword="null"/>.</exception>
    public static AsyncLock WriteLock(AsyncReaderWriterLock rwLock, bool upgrade = false)
        => new(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), upgrade ? Type.Upgrade : Type.WriteLock, false);

    /// <summary>
    /// Creates strong (exclusive) lock but doesn't acquire it.
    /// </summary>
    /// <param name="lock">The shared lock instance.</param>
    /// <returns>Exclusive lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    public static AsyncLock Exclusive(AsyncSharedLock @lock)
        => new(@lock ?? throw new ArgumentNullException(nameof(@lock)), Type.Strong, false);

    /// <summary>
    /// Creates weak lock but doesn't acquire it.
    /// </summary>
    /// <param name="lock">The shared lock instance.</param>
    /// <returns>Weak lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    public static AsyncLock Weak(AsyncSharedLock @lock)
        => new(@lock ?? throw new ArgumentNullException(nameof(@lock)), Type.Weak, false);

    /// <summary>
    /// Acquires the lock asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The task returning the acquired lock holder.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public readonly ValueTask<Holder> AcquireAsync(CancellationToken token) => AcquireAsync(InfiniteTimeSpan, token);

    [Conditional("DEBUG")]
    private readonly void AssertLockType()
    {
        switch (type)
        {
            case Type.Exclusive:
                Debug.Assert(lockedObject is AsyncExclusiveLock);
                break;
            case Type.ReadLock or Type.WriteLock or Type.Upgrade:
                Debug.Assert(lockedObject is AsyncReaderWriterLock);
                break;
            case Type.Semaphore:
                Debug.Assert(lockedObject is SemaphoreSlim);
                break;
            case Type.Strong or Type.Weak:
                Debug.Assert(lockedObject is AsyncSharedLock);
                break;
            default:
                Debug.Assert(lockedObject is null);
                break;
        }
    }

    private readonly ValueTask AcquireCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        AssertLockType();

        return type switch
        {
            Type.Exclusive => As<AsyncExclusiveLock>(lockedObject).AcquireAsync(timeout, token),
            Type.ReadLock => As<AsyncReaderWriterLock>(lockedObject).EnterReadLockAsync(timeout, token),
            Type.WriteLock => As<AsyncReaderWriterLock>(lockedObject).EnterWriteLockAsync(timeout, token),
            Type.Upgrade => As<AsyncReaderWriterLock>(lockedObject).UpgradeToWriteLockAsync(timeout, token),
            Type.Semaphore => CheckOnTimeoutAsync(As<SemaphoreSlim>(lockedObject).WaitAsync(timeout, token)),
            Type.Strong => As<AsyncSharedLock>(lockedObject).AcquireAsync(true, timeout, token),
            Type.Weak => As<AsyncSharedLock>(lockedObject).AcquireAsync(false, timeout, token),
            _ => ValueTask.CompletedTask,
        };

        static async ValueTask CheckOnTimeoutAsync(Task<bool> task)
        {
            if (!await task.ConfigureAwait(false))
                throw new TimeoutException();
        }
    }

    /// <summary>
    /// Acquires the lock asynchronously.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The task returning the acquired lock holder.</returns>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public readonly async ValueTask<Holder> AcquireAsync(TimeSpan timeout, CancellationToken token = default)
    {
        await AcquireCoreAsync(timeout, token).ConfigureAwait(false);
        return CreateHolder();
    }

    private readonly ValueTask<bool> TryAcquireCoreAsync(TimeSpan timeout, CancellationToken token)
    {
        AssertLockType();

        return type switch
        {
            Type.Exclusive => As<AsyncExclusiveLock>(lockedObject).TryAcquireAsync(timeout, token),
            Type.ReadLock => As<AsyncReaderWriterLock>(lockedObject).TryEnterReadLockAsync(timeout, token),
            Type.Upgrade => As<AsyncReaderWriterLock>(lockedObject).TryUpgradeToWriteLockAsync(timeout, token),
            Type.WriteLock => As<AsyncReaderWriterLock>(lockedObject).TryEnterWriteLockAsync(timeout, token),
            Type.Semaphore => new(As<SemaphoreSlim>(lockedObject).WaitAsync(timeout, token)),
            Type.Strong => As<AsyncSharedLock>(lockedObject).TryAcquireAsync(true, timeout, token),
            Type.Weak => As<AsyncSharedLock>(lockedObject).TryAcquireAsync(false, timeout, token),
            _ => new(false),
        };
    }

    /// <summary>
    /// Tries to acquire the lock asynchronously.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The task returning the acquired lock holder; or empty lock holder if lock has not been acquired.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public readonly async ValueTask<Holder> TryAcquireAsync(TimeSpan timeout, CancellationToken token = default)
        => await TryAcquireCoreAsync(timeout, token).ConfigureAwait(false) ? CreateHolder() : default;

    /// <summary>
    /// Tries to acquire lock asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to abort acquisition operation.</param>
    /// <returns>The task returning the acquired lock holder; or empty lock holder if operation was canceled.</returns>
    [Obsolete("Use AcquireAsync(CancellationToken) instead.")]
    public readonly ValueTask<Holder> TryAcquireAsync(CancellationToken token)
        => TryAcquireAsync(InfiniteTimeSpan, token);

    /// <summary>
    /// Destroy this lock and dispose underlying lock object if it is owned by the given lock.
    /// </summary>
    /// <remarks>
    /// If the given lock is an owner of the underlying lock object then this method will call <see cref="IDisposable.Dispose()"/> on it;
    /// otherwise, the underlying lock object will not be destroyed.
    /// As a result, this lock is not usable after calling of this method.
    /// </remarks>
    public void Dispose()
    {
        if (owner && lockedObject is IDisposable disposable)
            disposable.Dispose();
        this = default;
    }

    /// <summary>
    /// Destroy this lock and asynchronously dispose underlying lock object if it is owned by the given lock.
    /// </summary>
    /// <remarks>
    /// If the given lock is an owner of the underlying lock object then this method will
    /// call <see cref="IAsyncDisposable.DisposeAsync"/> or <see cref="IDisposable.Dispose()"/> on it;
    /// otherwise, the underlying lock object will not be destroyed.
    /// As a result, this lock is not usable after calling of this method.
    /// </remarks>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public ValueTask DisposeAsync()
    {
        ValueTask result = default;
        if (owner)
        {
            switch (lockedObject)
            {
                case IAsyncDisposable disposable:
                    result = disposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }

        this = default;
        return result;
    }

    private readonly bool Equals(in AsyncLock other)
        => type == other.type && ReferenceEquals(lockedObject, other.lockedObject) && owner == other.owner;

    /// <summary>
    /// Determines whether this lock object is the same as other lock.
    /// </summary>
    /// <param name="other">Other lock to compare.</param>
    /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
    public readonly bool Equals(AsyncLock other) => Equals(in other);

    /// <summary>
    /// Determines whether this lock object is the same as other lock.
    /// </summary>
    /// <param name="other">Other lock to compare.</param>
    /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
    public override readonly bool Equals([NotNullWhen(true)] object? other) => other is AsyncLock @lock && Equals(in @lock);

    /// <summary>
    /// Computes hash code of this lock.
    /// </summary>
    /// <returns>The hash code of this lock.</returns>
    public override readonly int GetHashCode() => HashCode.Combine(lockedObject, type, owner);

    /// <summary>
    /// Returns actual type of this lock in the form of the string.
    /// </summary>
    /// <returns>The actual type of this lock.</returns>
    public override readonly string ToString() => type.ToString();

    /// <summary>
    /// Determines whether two locks are the same.
    /// </summary>
    /// <param name="first">The first lock to compare.</param>
    /// <param name="second">The second lock to compare.</param>
    /// <returns><see langword="true"/>, if both are the same; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in AsyncLock first, in AsyncLock second)
        => first.Equals(in second);

    /// <summary>
    /// Determines whether two locks are not the same.
    /// </summary>
    /// <param name="first">The first lock to compare.</param>
    /// <param name="second">The second lock to compare.</param>
    /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in AsyncLock first, in AsyncLock second)
        => !first.Equals(in second);
}