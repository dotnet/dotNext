using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Numerics;
using Threading;
using Threading.Tasks;

/// <summary>
/// Represents concurrent cache optimized for random access, which keeps the entries in unmanaged memory.
/// </summary>
/// <remarks>
/// The cache entries are not visible to GC, so the size of the cache doesn't affect GC pauses.
/// The key is a sequence of bytes copied to the cache entry. The value of the entry cannot be modified
/// after insertion. All the sessions must be disposed. The cache evicts older records on overflow
/// using SIEVE algorithm.
/// </remarks>
/// <typeparam name="TValue">The type of the values.</typeparam>
public sealed partial class RandomAccessCache<TValue> : Disposable, IAsyncDisposable
    where TValue : unmanaged
{
    private readonly Bucket[] buckets; // pinned, so the buckets can be accessed through pointers
    private readonly FastMod fastMod;
    [SuppressMessage("Usage", "CA2213", Justification = "Disposed by the eviction thread after the teardown")]
    private readonly AsyncParkingLot parkingLot;
    private readonly CancellationTokenMultiplexer cancellationTokens;
    private readonly CancellationToken lifetimeToken;
    private readonly Task evictionTask;
    
    [SuppressMessage("Usage", "CA2213", Justification = "Disposed by the eviction thread after the teardown")]
    private CancellationTokenSource? lifetimeSource;

    /// <summary>
    /// Initializes a new cache.
    /// </summary>
    /// <param name="capacity">Maximum cache size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than or equal to zero.</exception>
    public RandomAccessCache(int capacity)
        : this(capacity, RandomAccessCache.DefaultConcurrencyLevel)
    {
    }

    /// <summary>
    /// Initializes a new cache.
    /// </summary>
    /// <param name="capacity">Maximum cache size.</param>
    /// <param name="concurrencyLevel">
    /// The expected number of concurrent writers. It determines the number of internal wait queues
    /// for the writers waiting for the same bucket, and doesn't limit the number of concurrent writers.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than or equal to zero;
    /// or <paramref name="concurrencyLevel"/> is less than or equal to zero.
    /// </exception>
    public RandomAccessCache(int capacity, int concurrencyLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLevel);
        
        Capacity = capacity = int.GetPrime(capacity);
        fastMod = new((uint)capacity);
        buckets = GC.AllocateArray<Bucket>(capacity, pinned: true);
        parkingLot = new(concurrencyLevel);
        cancellationTokens = new() { MaximumRetained = concurrencyLevel };
        lifetimeSource = new();
        lifetimeToken = lifetimeSource.Token;
        epoch = new() { NativeResourceManager = this };

        evictionTask = DoEvictionAsync();
    }

    /// <summary>
    /// Gets or sets the hash algorithm for the key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public IEqualityComparer<ReadOnlySpan<byte>> EqualityComparer
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = IEqualityComparer<ReadOnlySpan<byte>>.Default;

    /// <summary>
    /// Gets the capacity of this cache.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets or sets a callback that can be used to clean up the evicted value.
    /// </summary>
    /// <remarks>
    /// The callback is invoked once for every cache record when the record is no longer available:
    /// it is evicted or invalidated, and all the sessions associated with the record are closed.
    /// The callback is invoked for the remaining records when the cache is disposed as well.
    /// If the cache is not disposed explicitly, the finalizer releases the memory without calling the callback.
    /// The key is valid only during the call. The callback should not throw: the exception propagates
    /// to the caller that released the record, which can be the background eviction process.
    /// </remarks>
    public ReadOnlySpanAction<byte, TValue>? Eviction { get; init; }

    private int IndexOf(int hashCode) => (int)fastMod.GetRemainder((uint)hashCode);

    /// <summary>
    /// Tries to read the cached record.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the session. However,
    /// the value can be evicted immediately after. The caller must dispose session.
    /// </remarks>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="session">A session that can be used to read the cached record.</param>
    /// <returns><see langword="true"/> if the record is available for reading and the session is active; otherwise, <see langword="false"/>.</returns>
    public unsafe bool TryRead(ReadOnlySpan<byte> key, out ReadSession session)
    {
        var hashCode = EqualityComparer.GetHashCode(key);

        // the epoch protects the traversed entries from reclamation
        using (epoch.Enter())
        {
            if (!IsDisposingOrDisposed)
            {
                for (var current = Volatile.Read(&this[IndexOf(hashCode)]->First);
                     current is not null;
                     current = Volatile.Read(&current->NextInBucket))
                {
                    if (current->Matches(key, hashCode, EqualityComparer) && current->Visit() && current->TryAcquire())
                    {
                        session = new(this, current);
                        return true;
                    }
                }
            }
        }

        session = default;
        return false;
    }

    /// <summary>
    /// Determines whether the cache entry associated with the specified key exists in the cache.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the cache entry associated with <paramref name="key"/> exists in the cache; otherwise, <see langword="false"/>.</returns>
    public unsafe bool Contains(ReadOnlySpan<byte> key)
    {
        var hashCode = EqualityComparer.GetHashCode(key);

        using (epoch.Enter())
        {
            if (!IsDisposingOrDisposed)
            {
                for (var current = Volatile.Read(&this[IndexOf(hashCode)]->First);
                     current is not null;
                     current = Volatile.Read(&current->NextInBucket))
                {
                    if (current->Matches(key, hashCode, EqualityComparer) && !current->IsDead)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Opens a session that can be used to insert the value associated with the key.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the returned session. However,
    /// the value can be evicted immediately after. The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to read or insert the cache record.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadWriteSession<TKey>> ChangeAsync<TKey>(TKey key, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return ChangeAsync(key, hashCode, IndexOf(hashCode), Timeout.Infinite, replace: false, token);
    }

    /// <summary>
    /// Opens a session synchronously that can be used to insert the value associated with the key.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the returned session. However,
    /// the value can be evicted immediately after. The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to read or insert the cache record.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ReadWriteSession<TKey> Change<TKey>(TKey key, TimeSpan timeout, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return ChangeAsync(key, hashCode, IndexOf(hashCode), new(timeout), replace: false, token).Wait();
    }

    /// <summary>
    /// Replaces the cache record associated with the specified key.
    /// </summary>
    /// <remarks>
    /// The existing record is removed, and the returned session can be used to insert a new value.
    /// If the session is disposed without inserting a value, the cache has no record associated with the key.
    /// The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to insert the cache record.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadWriteSession<TKey>> ReplaceAsync<TKey>(TKey key, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return ChangeAsync(key, hashCode, IndexOf(hashCode), Timeout.Infinite, replace: true, token);
    }

    /// <summary>
    /// Replaces the cache record associated with the specified key synchronously.
    /// </summary>
    /// <remarks>
    /// The existing record is removed, and the returned session can be used to insert a new value.
    /// If the session is disposed without inserting a value, the cache has no record associated with the key.
    /// The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to insert the cache record.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ReadWriteSession<TKey> Replace<TKey>(TKey key, TimeSpan timeout, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return ChangeAsync(key, hashCode, IndexOf(hashCode), new(timeout), replace: true, token).Wait();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ReadWriteSession<TKey>> ChangeAsync<TKey>(TKey key, int hashCode, int index, Timeout timeout, bool replace, CancellationToken token)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var cts = cancellationTokens.Combine(token, lifetimeToken);
        var lockTaken = false;
        try
        {
            await AcquireBucketAsync(index, timeout, cts.Token).ConfigureAwait(false);
            lockTaken = true;

            if (replace)
            {
                Invalidate(key.Invoke(), hashCode, index);
            }
            else if (OpenSessionForExistingEntry(key, hashCode, index) is { IsEmpty: false } session)
            {
                return session;
            }

            // the lock is owned by the session
            lockTaken = false;
            return new(this, key, hashCode, index);
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, lifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                ReleaseBucket(index);

            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    private unsafe ReadWriteSession<TKey> OpenSessionForExistingEntry<TKey>(TKey key, int hashCode, int index)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var entry = FindAndCleanUp(this[index], key.Invoke(), hashCode);
        return entry is not null && entry->Visit() && entry->TryAcquire()
            ? new(this, entry)
            : default;
    }

    /// <summary>
    /// Invalidates the cache record associated with the specified key.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<bool> InvalidateAsync<TKey>(TKey key, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return InvalidateAsync(key, hashCode, IndexOf(hashCode), Timeout.Infinite, token);
    }

    /// <summary>
    /// Invalidates the cache record associated with the specified key synchronously.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public bool Invalidate<TKey>(TKey key, TimeSpan timeout, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return InvalidateAsync(key, hashCode, IndexOf(hashCode), new(timeout), token).Wait();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> InvalidateAsync<TKey>(TKey key, int hashCode, int index, Timeout timeout, CancellationToken token)
        where TKey  : ISupplier<ReadOnlySpan<byte>>
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var cts = cancellationTokens.Combine(token, lifetimeToken);
        var lockTaken = false;
        try
        {
            await AcquireBucketAsync(index, timeout, cts.Token).ConfigureAwait(false);
            lockTaken = true;
            return Invalidate(key.Invoke(), hashCode, index);
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, lifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                ReleaseBucket(index);

            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    private unsafe bool Invalidate(ReadOnlySpan<byte> key, int hashCode, int index)
        => Remove(key, hashCode, index, addReference: false) is not null;

    // The bucket lock must be held. If addReference is true, the caller owns a reference to the returned entry.
    // Otherwise, the returned pointer must not be dereferenced
    private unsafe Entry* Remove(ReadOnlySpan<byte> key, int hashCode, int index, bool addReference)
    {
        var bucket = this[index];
        Debug.Assert(bucket->LockState is not AsyncParkingLot.State.Free);
        
        var entry = FindAndCleanUp(bucket, key, hashCode);
        if (entry is null || !entry->MarkAsEvicted())
            return null;

        // the reference must be taken before the bucket chain releases its own reference
        if (addReference)
            entry->AddReference();

        // the eviction thread removes the entry from the SIEVE list, so it doesn't occupy the cache until the hand reaches it
        NotifyRemoved(entry);

        // unlink the evicted entry
        CleanUp(bucket);
        return entry;
    }

    /// <summary>
    /// Tries to remove the cache record associated with the specified key.
    /// </summary>
    /// <remarks>
    /// The removed record remains available for reading until the returned session is disposed.
    /// The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// The session that can be used to read the removed cache record;
    /// or <see langword="null"/> if there is no record associated with <paramref name="key"/>.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadSession?> TryRemoveAsync<TKey>(TKey key, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        return TryRemoveAsync(key, hashCode, IndexOf(hashCode), Timeout.Infinite, token);
    }

    /// <summary>
    /// Tries to remove the cache record associated with the specified key synchronously.
    /// </summary>
    /// <remarks>
    /// The removed record remains available for reading until the returned session is disposed.
    /// The caller must dispose the session.
    /// </remarks>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="session">The session that can be used to read the removed cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the record associated with <paramref name="key"/> exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public bool TryRemove<TKey>(TKey key, out ReadSession session, TimeSpan timeout, CancellationToken token = default)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        var hashCode = EqualityComparer.GetHashCode(key.Invoke());
        var result = TryRemoveAsync(key, hashCode, IndexOf(hashCode), new(timeout), token).Wait();
        session = result.GetValueOrDefault();
        return result.HasValue;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ReadSession?> TryRemoveAsync<TKey>(TKey key, int hashCode, int index, Timeout timeout, CancellationToken token)
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var cts = cancellationTokens.Combine(token, lifetimeToken);
        var lockTaken = false;
        try
        {
            await AcquireBucketAsync(index, timeout, cts.Token).ConfigureAwait(false);
            lockTaken = true;
            return TryRemove(key.Invoke(), hashCode, index);
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, lifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                ReleaseBucket(index);

            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    private unsafe ReadSession? TryRemove(ReadOnlySpan<byte> key, int hashCode, int index)
    {
        var entry = Remove(key, hashCode, index, addReference: true);
        return entry is not null ? new ReadSession(this, entry) : null;
    }

    /// <summary>
    /// Invalidates the entire cache.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The state of the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask InvalidateAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var cts = cancellationTokens.Combine(token, lifetimeToken);
        var lockCount = 0;
        try
        {
            // the same order as the teardown
            for (; lockCount < Capacity; lockCount++)
            {
                await AcquireBucketAsync(lockCount, cts.Token).ConfigureAwait(false);
            }

            InvalidateAll();
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, lifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            for (var i = 0; i < lockCount; i++)
            {
                ReleaseBucket(i);
            }

            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    // all the bucket locks are held
    private unsafe void InvalidateAll()
    {
        for (var i = 0; i < Capacity; i++)
        {
            var bucket = this[i];
            for (var entry = bucket->First; entry is not null; entry = entry->NextInBucket)
            {
                entry->MarkAsEvicted();
            }

            CleanUp(bucket);
        }

        // a single signal for all the entries: the entries inserted after the invalidation are enqueued after it
        NotifyRemovedAll();
    }

    // the bucket lock must be held
    private unsafe void Insert(ReadOnlySpan<byte> key, int hashCode, int index, in TValue value)
    {
        var bucket = this[index];
        Debug.Assert(bucket->LockState is not AsyncParkingLot.State.Free);
        
        if (bucket->Added is not null)
            throw new InvalidOperationException();

        // the bucket chain and the promotion queue own the entry
        var entry = Entry.Allocate(key, hashCode, in value);
        OnAllocated(entry);
        entry->NextInBucket = bucket->First;
        Volatile.Write(&bucket->First, entry); // publish the initialized entry to the lock-free readers
        bucket->Added = entry;
        Enqueue(entry);
    }

    private unsafe void ReleaseSessionLock(int index)
    {
        this[index]->Added = null;
        ReleaseBucket(index);
    }

    private unsafe ValueTask ReleaseSessionLockAndWaitForPromotionAsync(int index)
    {
        var bucket = this[index];
        var added = bucket->Added;
        bucket->Added = null;

        // the entry must be alive until the waiter observes the promotion
        if (added is not null)
            added->AddReference();

        ReleaseBucket(index);
        return added is null ? ValueTask.CompletedTask : WaitForPromotionAsync((nint)added);
    }
    
    private void Cancel()
    {
        if (Interlocked.Exchange(ref lifetimeSource, null) is { } cts)
        {
            try
            {
                cts.Cancel(throwOnFirstException: false);
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();

    /// <inheritdoc/>
    protected override ValueTask DisposeAsyncCore()
    {
        Dispose(disposing: true);
        return new(evictionTask);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // the eviction loop releases the unmanaged memory when all the readers and writers are gone
            Cancel();
            signal.Set();
        }
        else if (!IsDisposingOrDisposed)
        {
            // Finalizer of the cache that was never disposed. DisposeAsync calls this method as well,
            // but the cache is already disposing at that point, and the teardown released the memory
            ReleaseAbandonedMemory();
        }

        base.Dispose(disposing);
    }

    private unsafe Bucket* this[int index]
    {
        get
        {
            Debug.Assert((uint)index < (uint)Capacity);

            return (Bucket*)Unsafe.AsPointer(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index));
        }
    }

    private unsafe ValueTask AcquireBucketAsync(int index, CancellationToken token)
        => parkingLot.ParkAsync(index, ref this[index]->LockState, userData: null, token);

    private unsafe ValueTask AcquireBucketAsync(int index, in Timeout timeout, CancellationToken token)
    {
        if (timeout.IsInfinite)
            return AcquireBucketAsync(index, token);

        var task = parkingLot.ParkAsync(index, ref this[index]->LockState, userData: null, timeout.RemainingTime, token);
        if (!task.IsCompletedSuccessfully)
            return ThrowIfTimedOutAsync(task);

        return task.Result ? ValueTask.CompletedTask : ValueTask.FromException(new TimeoutException());
    }

    private static async ValueTask ThrowIfTimedOutAsync(ValueTask<bool> task)
    {
        if (!await task.ConfigureAwait(false))
            throw new TimeoutException();
    }

    private unsafe bool TryAcquireBucket(int index)
        => AsyncParkingLot.TryPark(ref this[index]->LockState);

    private unsafe void ReleaseBucket(int index)
        => parkingLot.Unpark(index, ref this[index]->LockState);

    // the bucket lock must be held, removes the dead entries
    private unsafe Entry* FindAndCleanUp(Bucket* bucket, ReadOnlySpan<byte> key, int hashCode)
    {
        Debug.Assert(bucket->LockState is not AsyncParkingLot.State.Free);
        
        Entry* result = null;
        var location = &bucket->First;
        for (Entry* current; (current = *location) is not null;)
        {
            if (current->IsDead)
            {
                Unlink(location, current);
            }
            else
            {
                if (result is null && current->Matches(key, hashCode, EqualityComparer))
                    result = current;

                location = &current->NextInBucket;
            }
        }

        return result;
    }

    // the bucket lock must be held
    private unsafe void CleanUp(Bucket* bucket)
    {
        Debug.Assert(bucket->LockState is not AsyncParkingLot.State.Free);
        
        var location = &bucket->First;
        for (Entry* current; (current = *location) is not null;)
        {
            if (current->IsDead)
            {
                Unlink(location, current);
            }
            else
            {
                location = &current->NextInBucket;
            }
        }
    }

    private unsafe void Unlink(Entry** location, Entry* entry)
    {
        // the readers may still traverse the entry, so its own link remains intact
        Volatile.Write(location, entry->NextInBucket);
        Release(entry); // the reference owned by the bucket chain
    }

    private unsafe void TryCleanUpBucket(int hashCode)
    {
        var index = IndexOf(hashCode);
        if (TryAcquireBucket(index))
        {
            try
            {
                CleanUp(this[index]);
            }
            finally
            {
                ReleaseBucket(index);
            }
        }
    }

    /// <summary>
    /// Represents a session that can be used to read the cache record value.
    /// </summary>
    /// <remarks>
    /// While session alive, the record cannot be reclaimed.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ReadSession : IDisposable
    {
        private readonly RandomAccessCache<TValue>? cache;
        private readonly Entry* entry;

        internal ReadSession(RandomAccessCache<TValue> cache, Entry* entry)
        {
            this.cache = cache;
            this.entry = entry;
        }

        /// <summary>
        /// Gets the key associated with this cache entry.
        /// </summary>
        public ReadOnlySpan<byte> Key => entry is null ? ReadOnlySpan<byte>.Empty : entry->Key;

        /// <summary>
        /// Gets the value of the cache record.
        /// </summary>
        public TValue Value => entry->Value;

        /// <summary>
        /// Gets a reference to the value of the cache record.
        /// </summary>
        public ref readonly TValue ValueRef => ref entry->Value;

        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <remarks>
        /// This method should be called only once.
        /// </remarks>
        public void Dispose()
        {
            if (entry is not null)
                cache?.Release(entry);
        }
    }

    /// <summary>
    /// Represents a session that can be used to read or insert the cache record value.
    /// </summary>
    /// <remarks>
    /// If the record exists, the session prevents it from reclamation. Otherwise, the session holds the lock
    /// that prevents the concurrent insertion of the record with the same key.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ReadWriteSession<TKey> : IDisposable, IAsyncDisposable
        where TKey : ISupplier<ReadOnlySpan<byte>>
    {
        private readonly RandomAccessCache<TValue>? cache;
        private readonly Entry* entry; // the existing entry, or null if the session holds the bucket lock
        private readonly TKey? key;
        private readonly int hashCode, index;

        internal ReadWriteSession(RandomAccessCache<TValue> cache, Entry* entry)
        {
            this.cache = cache;
            this.entry = entry;
        }

        internal ReadWriteSession(RandomAccessCache<TValue> cache, TKey key, int hashCode, int index)
        {
            this.cache = cache;
            this.key = key;
            this.hashCode = hashCode;
            this.index = index;
        }

        [MemberNotNullWhen(false, nameof(cache))]
        internal bool IsEmpty => cache is null;

        /// <summary>
        /// Tries to get the value of the cache record.
        /// </summary>
        /// <param name="result">The value of the cache record.</param>
        /// <returns><see langword="true"/> if value exists; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(out TValue result)
        {
            if (entry is not null)
            {
                result = entry->Value;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Inserts the cache record value.
        /// </summary>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="InvalidOperationException">The session is invalid; the record already exists; the value is inserted more than once.</exception>
        public void SetValue(in TValue value)
        {
            if (IsEmpty || entry is not null)
                throw new InvalidOperationException();

            Debug.Assert(key is not null);
            cache.Insert(key.Invoke(), hashCode, index, in value);
        }

        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <remarks>
        /// This method should be called only once. The method doesn't wait for the cache entry promotion.
        /// </remarks>
        public void Dispose()
        {
            if (IsEmpty)
            {
                // nothing to do
            }
            else if (entry is not null)
            {
                cache.Release(entry);
            }
            else
            {
                cache.ReleaseSessionLock(index);
            }
        }

        /// <summary>
        /// Closes the session and waits for the promotion of the inserted entry.
        /// </summary>
        /// <remarks>
        /// This method should be called only once.
        /// </remarks>
        /// <returns>The task representing asynchronous state of the cache entry promotion.</returns>
        public ValueTask DisposeAsync()
        {
            var task = ValueTask.CompletedTask;
            if (entry is not null)
            {
                Dispose();
            }
            else if (!IsEmpty)
            {
                task = cache.ReleaseSessionLockAndWaitForPromotionAsync(index);
            }

            return task;
        }
    }
}