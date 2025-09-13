using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Threading.Tasks;

namespace DotNext.Runtime.Caching;

using Numerics;
using Threading;

/// <summary>
/// Represents concurrent cache optimized for random access.
/// </summary>
/// <remarks>
/// The cache evicts older records on overflow.
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
public partial class RandomAccessCache<TKey, TValue> : Disposable, IAsyncDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
    where TValue : notnull
{
    private readonly CancellationToken lifetimeToken;
    private readonly IEqualityComparer<TKey>? keyComparer;
    private readonly bool growable;
    private readonly CancellationTokenMultiplexer cancellationTokens;
    private Task evictionTask;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? lifetimeSource;

    /// <summary>
    /// Initializes a new cache.
    /// </summary>
    /// <param name="cacheCapacity">Maximum cache size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cacheCapacity"/> is less than or equal to zero.</exception>
    public RandomAccessCache(int cacheCapacity)
        : this(cacheCapacity, collisionThreshold: int.MaxValue)
    {
    }

    private protected RandomAccessCache(int dictionarySize, int collisionThreshold,
        [CallerArgumentExpression(nameof(dictionarySize))]
        string? dictionarySizeName = null,
        [CallerArgumentExpression(nameof(collisionThreshold))]
        string? thresholdName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dictionarySize, dictionarySizeName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(collisionThreshold, thresholdName);

        dictionarySize = PrimeNumber.GetPrime(dictionarySize);
        cancellationTokens = new() { MaximumRetained = 100 };
        maxCacheCapacity = (growable = collisionThreshold is not int.MaxValue)
            ? collisionThreshold
            : dictionarySize;

        buckets = new(dictionarySize);
        lifetimeSource = new();
        lifetimeToken = lifetimeSource.Token;
        queueHead = queueTail = new FakeKeyValuePair();

        evictionTask = DoEvictionAsync(completionSource = new());
    }

    private bool ResizeDesired(in Bucket bucket)
        => growable && bucket.CollisionCount >= maxCacheCapacity;

    /// <summary>
    /// Gets or sets a callback that can be used to clean up the evicted value.
    /// </summary>
    public Action<TKey, TValue>? Eviction { get; init; }

    /// <summary>
    /// Gets or sets key comparer.
    /// </summary>
    public IEqualityComparer<TKey>? KeyComparer
    {
        get => keyComparer;
        init => keyComparer = ReferenceEquals(value, EqualityComparer<TKey>.Default) ? null : value;
    }

    /// <summary>
    /// Gets the capacity of this cache.
    /// </summary>
    public int Capacity => buckets.Count;
    
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ReadWriteSession> ChangeAsync(TKey key, Timeout timeout, CancellationToken token)
    {
        var hashCode = keyComparer?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        var cts = cancellationTokens.Combine([token, lifetimeToken]);
        var bucketLock = default(AsyncExclusiveLock);
        try
        {
            for (BucketList bucketsCopy;; await GrowAsync(bucketsCopy, timeout, cts.Token).ConfigureAwait(false))
            {
                Bucket.Ref bucket;

                bucketsCopy = buckets;
                for (BucketList newCopy;; bucketsCopy = newCopy)
                {
                    bucketsCopy.GetByHash(hashCode, out bucket);
                    await bucket.Value.Lock.AcquireAsync(timeout.GetRemainingTimeOrZero(), cts.Token).ConfigureAwait(false);
                    bucketLock = bucket.Value.Lock;

                    newCopy = buckets;
                    if (ReferenceEquals(newCopy, bucketsCopy))
                        break;

                    bucketLock.Release();
                    bucketLock = null;
                }

                if (bucket.Value.TryGet<AcquisitionVisitor>(keyComparer, key, hashCode) is { } valueHolder)
                    return new(this, valueHolder);

                var bucketLockCopy = bucketLock;
                bucketLock = null;
                if (!ResizeDesired(in bucket.Value))
                    return new(this, in bucket, bucketLockCopy, key, hashCode);

                bucketLockCopy.Release();
            }
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
            bucketLock?.Release();
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a session that can be used to modify the value associated with the key.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the returned session. However,
    /// the value can be evicted immediately after. The caller must dispose session.
    /// </remarks>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to read or modify the cache record.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadWriteSession> ChangeAsync(TKey key, CancellationToken token = default)
        => ChangeAsync(key, Timeout.Infinite, token);

    /// <summary>
    /// Opens a session synchronously that can be used to modify the value associated with the key.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the returned session. However,
    /// the value can be evicted immediately after. The caller must dispose session.
    /// </remarks>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to read or modify the cache record.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ReadWriteSession Change(TKey key, TimeSpan timeout, CancellationToken token = default)
        => ChangeAsync(key, new(timeout), token).Wait();
    
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ReadWriteSession> ReplaceAsync(TKey key, Timeout timeout, CancellationToken token)
    {
        var hashCode = keyComparer?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        var cts = cancellationTokens.Combine([token, lifetimeToken]);
        var bucketLock = default(AsyncExclusiveLock);
        try
        {
            for (BucketList bucketsCopy;; await GrowAsync(bucketsCopy, timeout, cts.Token).ConfigureAwait(false))
            {
                Bucket.Ref bucket;

                bucketsCopy = buckets;
                for (BucketList newCopy;; bucketsCopy = newCopy)
                {
                    bucketsCopy.GetByHash(hashCode, out bucket);
                    await bucket.Value.Lock.AcquireAsync(timeout.GetRemainingTimeOrZero(), cts.Token).ConfigureAwait(false);
                    bucketLock = bucket.Value.Lock;

                    newCopy = buckets;
                    if (ReferenceEquals(newCopy, bucketsCopy))
                        break;

                    bucketLock.Release();
                    bucketLock = null;
                }

                var bucketLockCopy = bucketLock;
                bucketLock = null;
                if (!ResizeDesired(in bucket.Value))
                {
                    if (bucket.Value.TryRemove(keyComparer, key, hashCode) is { } removedPair && removedPair.ReleaseCounter() is false)
                        OnRemoved(removedPair);

                    return new(this, in bucket, bucketLockCopy, key, hashCode);
                }

                bucketLockCopy.Release();
            }
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
            bucketLock?.Release();
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Replaces the cache entry associated with the specified key.
    /// </summary>
    /// <param name="key">The key associated with the cache entry.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to modify the cache record.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadWriteSession> ReplaceAsync(TKey key, CancellationToken token = default)
        => ReplaceAsync(key, Timeout.Infinite, token);

    /// <summary>
    /// Replaces the cache entry associated with the specified key synchronously.
    /// </summary>
    /// <param name="key">The key associated with the cache entry.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The session that can be used to modify the cache record.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ReadWriteSession Replace(TKey key, TimeSpan timeout, CancellationToken token = default)
        => ReplaceAsync(key, new(timeout), token).Wait();

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
    public bool TryRead(TKey key, out ReadSession session)
    {
        var keyComparerCopy = keyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        if (buckets.GetByHash(hashCode).TryGet<AcquisitionVisitor>(keyComparerCopy, key, hashCode) is { } valueHolder)
        {
            session = new(this, valueHolder);
            return true;
        }

        session = default;
        return false;
    }

    /// <summary>
    /// Determines whether the cache entry associated with the specified key exists in the cache.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the cache entry associated with <paramref name="key"/> exists in the cache; otherwise, <see langword="false"/>.</returns>
    public bool Contains(TKey key)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        
        var keyComparerCopy = keyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        return buckets.GetByHash(hashCode).TryGet<NotDeadFilter>(keyComparerCopy, key, hashCode) is not null;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ReadSession?> TryRemoveAsync(TKey key, TimeSpan timeout, CancellationToken token)
    {
        var hashCode = keyComparer?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        var cts = cancellationTokens.Combine([token, lifetimeToken]);
        var bucketLock = default(AsyncExclusiveLock);
        try
        {
            Bucket.Ref bucket;
            for (BucketList bucketsCopy = buckets, newCopy;; bucketsCopy = newCopy)
            {
                bucketsCopy.GetByHash(hashCode, out bucket);
                await bucket.Value.Lock.AcquireAsync(timeout, cts.Token).ConfigureAwait(false);
                bucketLock = bucket.Value.Lock;

                newCopy = buckets;
                if (ReferenceEquals(newCopy, bucketsCopy))
                    break;

                bucketLock.Release();
                bucketLock = null;
            }

            return bucket.Value.TryRemove(keyComparer, key, hashCode) is { } removedPair
                ? new ReadSession(this, removedPair)
                : null;
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
            bucketLock?.Release();
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tries to invalidate cache record associated with the provided key.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// The session that can be used to read the removed cache record;
    /// or <see langword="null"/> if there is no record associated with <paramref name="key"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<ReadSession?> TryRemoveAsync(TKey key, CancellationToken token = default)
        => TryRemoveAsync(key, Timeout.Infinite, token);

    /// <summary>
    /// Tries to invalidate cache record associated with the provided key synchronously.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="session">The session that can be used to read the removed cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the record associated with <paramref name="key"/> exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public bool TryRemove(TKey key, out ReadSession session, TimeSpan timeout, CancellationToken token = default)
    {
        var result = TryRemoveAsync(key, timeout, token).Wait();
        session = result.GetValueOrDefault();
        return result.HasValue;
    }
    
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> InvalidateAsync(TKey key, TimeSpan timeout, CancellationToken token)
    {
        var hashCode = keyComparer?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        var cts = cancellationTokens.Combine([token, lifetimeToken]);
        var bucketLock = default(AsyncExclusiveLock);
        KeyValuePair? removedPair;
        try
        {
            Bucket.Ref bucket;
            for (BucketList bucketsCopy = buckets, newCopy;; bucketsCopy = newCopy)
            {
                bucketsCopy.GetByHash(hashCode, out bucket);
                await bucket.Value.Lock.AcquireAsync(timeout, cts.Token).ConfigureAwait(false);
                bucketLock = bucket.Value.Lock;

                newCopy = buckets;
                if (ReferenceEquals(newCopy, bucketsCopy))
                    break;

                bucketLock.Release();
                bucketLock = null;
            }

            removedPair = bucket.Value.TryRemove(keyComparer, key, hashCode);
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
            bucketLock?.Release();
            await cts.DisposeAsync().ConfigureAwait(false);
        }
        
        if (removedPair is null)
        {
            return false;
        }

        if (removedPair.ReleaseCounter() is false)
        {
            OnRemoved(removedPair);
        }

        return true;
    }

    /// <summary>
    /// Invalidates the cache record associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ValueTask<bool> InvalidateAsync(TKey key, CancellationToken token = default)
        => InvalidateAsync(key, Timeout.Infinite, token);

    /// <summary>
    /// Invalidates the cache record associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public bool Invalidate(TKey key, TimeSpan timeout, CancellationToken token = default)
        => InvalidateAsync(key, timeout, token).Wait();

    /// <summary>
    /// Invalidates the entire cache.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public async ValueTask InvalidateAsync(CancellationToken token = default)
    {
        var cts = cancellationTokens.Combine([token, lifetimeToken]);
        var bucketsCopy = buckets;
        var lockCount = 0;
        try
        {
            AsyncExclusiveLock bucketLock;

            // take first lock
            for (BucketList newCopy;; bucketsCopy = newCopy)
            {
                bucketLock = bucketsCopy.GetByIndex(0).Lock;
                await bucketLock.AcquireAsync(cts.Token).ConfigureAwait(false);

                newCopy = buckets;
                if (ReferenceEquals(newCopy, bucketsCopy))
                    break;

                bucketLock.Release();
            }

            // take the rest of the locks
            for (lockCount = 1; lockCount < bucketsCopy.Count; lockCount++)
            {
                bucketLock = bucketsCopy.GetByIndex(lockCount).Lock;
                await bucketLock.AcquireAsync(cts.Token).ConfigureAwait(false);
            }

            // invalidate all buckets
            bucketsCopy.Invalidate(OnRemoved);
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
            bucketsCopy.Release(lockCount);
            await cts.DisposeAsync().ConfigureAwait(false);
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
        try
        {
            if (disposing)
            {
                Interlocked.Exchange(ref completionSource, null)?.Cancel();
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
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Gets an enumerator over the cache entries.
    /// </summary>
    /// <remarks>
    /// SIEVE algorithm is not scan-resistant, the returned enumerator doesn't update the recency for the entry. 
    /// </remarks>
    /// <returns>The enumerator over the cache entries.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => buckets.GetEnumerator(OnRemoved);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Represents a session that can be used to read the cache record value.
    /// </summary>
    /// <remarks>
    /// While session alive, the record cannot be evicted.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadSession : IDisposable
    {
        private readonly RandomAccessCache<TKey, TValue> cache;
        private readonly KeyValuePair valueHolder;

        internal ReadSession(RandomAccessCache<TKey, TValue> cache, KeyValuePair valueHolder)
        {
            this.cache = cache;
            this.valueHolder = valueHolder;
        }

        /// <summary>
        /// Gets the key associated with this cache entry.
        /// </summary>
        public TKey Key => valueHolder.Key;

        /// <summary>
        /// Gets the value of the cache record.
        /// </summary>
        public TValue Value => ValueRef;

        /// <summary>
        /// Gets a reference to a value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ref readonly TValue ValueRef => ref GetValue(valueHolder);

        /// <summary>
        /// Closes the session.
        /// </summary>
        public void Dispose()
        {
            if (valueHolder?.ReleaseCounter() is false)
            {
                cache.OnRemoved(valueHolder);
            }
        }
    }

    /// <summary>
    /// Represents a session that can be used to read, modify or promote the cache record value.
    /// </summary>
    /// <remarks>
    /// While session alive, the record cannot be evicted.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadWriteSession : IDisposable, IAsyncDisposable
    {
        private readonly RandomAccessCache<TKey, TValue> cache;
        private readonly object lockOrValueHolder; // AsyncExclusiveLock or KeyValuePair
        private readonly Bucket.Ref bucket;
        private readonly TKey key;
        private readonly int hashCode;

        internal ReadWriteSession(RandomAccessCache<TKey, TValue> cache, in Bucket.Ref bucket, AsyncExclusiveLock bucketLock, TKey key, int hashCode)
        {
            this.cache = cache;
            lockOrValueHolder = bucketLock;
            this.key = key;
            this.hashCode = hashCode;
            this.bucket = bucket;
        }

        internal ReadWriteSession(RandomAccessCache<TKey, TValue> cache, KeyValuePair valueHolder)
        {
            this.cache = cache;
            lockOrValueHolder = valueHolder;
            key = valueHolder.Key;
            hashCode = valueHolder.KeyHashCode;
        }

        /// <summary>
        /// Tries to get the value of the cache record.
        /// </summary>
        /// <param name="result">The value of the cache record.</param>
        /// <returns><see langword="true"/> if value exists; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue([MaybeNullWhen(false)] out TValue result)
        {
            if (lockOrValueHolder is KeyValuePair valueHolder)
            {
                result = GetValue(valueHolder);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Gets a reference to a value.
        /// </summary>
        /// <value>A reference to a value; or <see langowrd="null"/> reference.</value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ref readonly TValue ValueRefOrNullRef
            => ref lockOrValueHolder is KeyValuePair valueHolder
                ? ref GetValue(valueHolder)
                : ref Unsafe.NullRef<TValue>();

        /// <summary>
        /// Promotes or modifies the cache record value.
        /// </summary>
        /// <param name="value">The value to promote or replace the existing value.</param>
        /// <exception cref="InvalidOperationException">The session is invalid; the value promotes more than once.</exception>
        public void SetValue(TValue value)
        {
            switch (lockOrValueHolder)
            {
                case AsyncExclusiveLock when bucket.Value.TryAdd(key, hashCode, value) is { } newPair:
                    cache.Promote(newPair);
                    break;
                case KeyValuePair existingPair when cache.growable is false:
                    RandomAccessCache<TKey, TValue>.SetValue(existingPair, value);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <remarks>
        /// This method should be called only once. The method doesn't wait for the cache entry promotion.
        /// </remarks>
        public void Dispose()
        {
            switch (lockOrValueHolder)
            {
                case AsyncExclusiveLock bucketLock:
                    bucket.Value.MarkAsReadyToAdd();
                    bucketLock.Release();
                    break;
                case KeyValuePair pair when pair.ReleaseCounter() is false:
                    cache.OnRemoved(pair);
                    break;
            }
        }
        
        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <remarks>
        /// This method should be called only once. It's recommended to use this method to ensure
        /// that the cache entry is promoted. This is useful when the cache is rate-limited. Otherwise,
        /// use <see cref="Dispose"/>, because concurrent promotions are handled by the single thread running
        /// in the background. Thus, awaiting of the returned task can cause contention over multiple writers.
        /// </remarks>
        /// <returns>The task representing asynchronous state of the cache entry promotion.</returns>
        public ValueTask DisposeAsync()
        {
            Task task;
            switch (lockOrValueHolder)
            {
                case AsyncExclusiveLock bucketLock:
                    task = bucket.Value.MarkAsReadyToAddAndGetTask();
                    bucketLock.Release(); // allow concurrent writers write to the same bucket without contention

                    // avoid deadlock if this method is called concurrently with Dispose() on the cache
                    if (!task.IsCompleted && cache.IsDisposingOrDisposed)
                        task = cache.DisposedTask;
                    
                    break;
                case KeyValuePair pair when pair.ReleaseCounter() is false:
                    cache.OnRemoved(pair);
                    goto default;
                default:
                    task = Task.CompletedTask;
                    break;
            }

            return new(task);
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct AcquisitionVisitor : IKeyValuePairVisitor
    {
        static bool IKeyValuePairVisitor.Visit(KeyValuePair pair) => pair.Visit() && pair.TryAcquireCounter();
    }
}

/// <summary>
/// Represents concurrent cache optimized for random access. The number of items in the cache is limited
/// by their weight. 
/// </summary>
/// <remarks>
/// In contrast to <see cref="RandomAccessCache{TKey,TValue}"/>, the size of weight-based cache can grow to preserve
/// <paramref name="collisionThreshold"/>. When the number of collisions reaches the threshold, the cache resizes
/// its internal structures and rehashes all the items. This process requires global lock of all items in the cache.
/// Thus, throughput degrades. To minimize a chance of resize, you can specify <paramref name="initialCapacity"/>
/// large enough by the cost of memory footprint or increase <paramref name="collisionThreshold"/> by the cost
/// of the contention. However, <see cref="RandomAccessCache{TKey,TValue}.TryRead"/> never causes contention even if the cache is resizing.
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="TWeight">The weight of the cache items.</typeparam>
/// <param name="initialCapacity">The expected max number of items within the cache.</param>
/// <param name="initialWeight">The initial weight value.</param>
/// <param name="collisionThreshold">
/// The maximum number of allowed hash collisions. Small number increases the memory footprint, because the cache minimizes the contention
/// for each key. Large number decreases the memory footprint by the increased chance of the lock contention.
/// If <see cref="int.MaxValue"/> is specified, then the cache doesn't grow.
/// </param>
public abstract class RandomAccessCache<TKey, TValue, TWeight>(int initialCapacity, TWeight initialWeight, int collisionThreshold) 
    : RandomAccessCache<TKey, TValue>(initialCapacity, collisionThreshold)
    where TKey : notnull
    where TValue : notnull
    where TWeight : notnull
{
    /// <summary>
    /// Adds a weight of the specified key/value pair to the total weight.
    /// </summary>
    /// <remarks>
    /// This method can be called concurrently with <see cref="RemoveWeight"/>.
    /// </remarks>
    /// <param name="total">The total weight of all items in the cache.</param>
    /// <param name="key">The key of the cache item.</param>
    /// <param name="value">The value of the cache item.</param>
    protected abstract void AddWeight(ref TWeight total, TKey key, TValue value);

    private protected sealed override void OnAdded(KeyValuePair promoted)
    {
        AddWeight(ref initialWeight, promoted.Key, GetValue(promoted));
        base.OnAdded(promoted);
    }

    /// <summary>
    /// Checks whether the current cache has enough capacity to promote the specified key/value pair.
    /// </summary>
    /// <param name="total">The total weight of all items in the cache.</param>
    /// <param name="key">The key of the cache item.</param>
    /// <param name="value">The value of the cache item.</param>
    /// <returns><see langword="true"/> if the cache must evict one or more items to place a new one; otherwise, <see langword="false"/>.</returns>
    protected abstract bool IsEvictionRequired(ref readonly TWeight total, TKey key, TValue value);

    private protected sealed override bool IsEvictionRequired(KeyValuePair promoted)
        => IsEvictionRequired(in initialWeight, promoted.Key, GetValue(promoted));

    /// <summary>
    /// Removes the weight of the specified cache item from the total weight.
    /// </summary>
    /// <remarks>
    /// This method can be called concurrently with <see cref="AddWeight"/>.
    /// </remarks>
    /// <param name="total">The total weight of all items in the cache.</param>
    /// <param name="key">The key of the cache item.</param>
    /// <param name="value">The value of the cache item.</param>
    protected abstract void RemoveWeight(ref TWeight total, TKey key, TValue value);

    private protected sealed override void OnRemoved(KeyValuePair demoted)
    {
        base.OnRemoved(demoted);
        RemoveWeight(ref initialWeight, demoted.Key, GetValue(demoted));
    }
}