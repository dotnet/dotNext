using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
public partial class RandomAccessCache<TKey, TValue> : Disposable, IAsyncDisposable
    where TKey : notnull
    where TValue : notnull
{
    private readonly CancellationToken lifetimeToken;
    private readonly Task evictionTask;
    private readonly IEqualityComparer<TKey>? keyComparer;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? lifetimeSource;

    /// <summary>
    /// Initializes a new cache.
    /// </summary>
    /// <param name="cacheSize">Maximum cache size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cacheSize"/> is less than or equal to zero.</exception>
    public RandomAccessCache(int cacheSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheSize);

        maxCacheSize = cacheSize;
        var dictionarySize = PrimeNumber.GetPrime(cacheSize);
        fastModMultiplier = IntPtr.Size is sizeof(ulong)
            ? PrimeNumber.GetFastModMultiplier((uint)dictionarySize)
            : default;

        Span.Initialize<Bucket>(buckets = new Bucket[dictionarySize]);

        lifetimeSource = new();
        lifetimeToken = lifetimeSource.Token;
        queueHead = queueTail = new FakeKeyValuePair();

        completionSource = new();
        evictionTask = DoEvictionAsync();
    }

    private string ObjectName => GetType().Name;

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
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<ReadOrWriteSession> ChangeAsync(TKey key, CancellationToken token = default)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);

        var cts = token.LinkTo(lifetimeToken);
        var lockTaken = false;
        try
        {
            await bucket.AcquireAsync(token).ConfigureAwait(false);
            lockTaken = true;

            if (bucket.Modify(keyComparerCopy, key, hashCode) is { } valueHolder)
                return new(this, valueHolder);

            lockTaken = false;
            return new(this, bucket, key, hashCode);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
        {
            throw cts.CancellationOrigin == lifetimeToken
                ? new ObjectDisposedException(ObjectName)
                : new OperationCanceledException(cts.CancellationOrigin);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
        {
            throw new ObjectDisposedException(ObjectName);
        }
        finally
        {
            cts?.Dispose();
            if (lockTaken)
                bucket.Release();
        }
    }

    /// <summary>
    /// Opens a session synchronously that can be used to modify the value associated with the key.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the returned session. However,
    /// the value can be evicted immediately after. The caller must dispose session.
    /// </remarks>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="timeout">The time to wait for the cache lock.</param>
    /// <returns>The session that can be used to read or modify the cache record.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public ReadOrWriteSession Change(TKey key, TimeSpan timeout)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);

        bool lockTaken;
        if (!(lockTaken = bucket.TryAcquire(timeout)))
            throw new TimeoutException();
        try
        {
            if (bucket.Modify(keyComparerCopy, key, hashCode) is { } valueHolder)
                return new(this, valueHolder);

            lockTaken = false;
            return new(this, bucket, key, hashCode);
        }
        finally
        {
            if (lockTaken)
                bucket.Release();
        }
    }

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
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);

        if (GetBucket(hashCode).TryGet(keyComparerCopy, key, hashCode) is { } valueHolder)
        {
            session = new(Eviction, valueHolder);
            return true;
        }

        session = default;
        return false;
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
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<ReadSession?> TryRemoveAsync(TKey key, CancellationToken token = default)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);

        var cts = token.LinkTo(lifetimeToken);
        var lockTaken = false;
        try
        {
            await bucket.AcquireAsync(token).ConfigureAwait(false);
            lockTaken = true;

            return bucket.TryRemove(keyComparerCopy, key, hashCode) is { } removedPair
                ? new ReadSession(Eviction, removedPair)
                : null;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
        {
            throw cts.CancellationOrigin == lifetimeToken
                ? new ObjectDisposedException(GetType().Name)
                : new OperationCanceledException(cts.CancellationOrigin);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
        {
            throw new ObjectDisposedException(ObjectName);
        }
        finally
        {
            if (lockTaken)
                bucket.Release();
        }
    }
    
    /// <summary>
    /// Tries to invalidate cache record associated with the provided key synchronously.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="timeout"></param>
    /// <param name="session">The session that can be used to read the removed cache record.</param>
    /// <returns><see langword="true"/> if the record associated with <paramref name="key"/> exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="TimeoutException">The internal lock cannot be acquired in timely manner.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public bool TryRemove(TKey key, TimeSpan timeout, out ReadSession session)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);
        
        if (!bucket.TryAcquire(timeout))
            throw new TimeoutException();
        try
        {
            session = bucket.TryRemove(keyComparerCopy, key, hashCode) is { } removedPair
                ? new(Eviction, removedPair)
                : default;
        }
        finally
        {
            bucket.Release();
        }

        return session.IsValid;
    }

    /// <summary>
    /// Invalidates the cache record associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the cache record to be removed.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<bool> InvalidateAsync(TKey key, CancellationToken token = default)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);

        var cts = token.LinkTo(lifetimeToken);
        var lockTaken = false;
        KeyValuePair? removedPair;
        try
        {
            await bucket.AcquireAsync(token).ConfigureAwait(false);
            lockTaken = true;
            removedPair = bucket.TryRemove(keyComparerCopy, key, hashCode);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
        {
            throw cts.CancellationOrigin == lifetimeToken
                ? new ObjectDisposedException(GetType().Name)
                : new OperationCanceledException(cts.CancellationOrigin);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
        {
            throw new ObjectDisposedException(ObjectName);
        }
        finally
        {
            if (lockTaken)
                bucket.Release();
        }

        if (removedPair is null)
        {
            return false;
        }

        if (removedPair.ReleaseCounter() is false)
        {
            Eviction?.Invoke(key, GetValue(removedPair));
            ClearValue(removedPair);
        }

        return true;
    }
    
    public bool Invalidate(TKey key, TimeSpan timeout)
    {
        var keyComparerCopy = KeyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? EqualityComparer<TKey>.Default.GetHashCode(key);
        var bucket = GetBucket(hashCode);
        
        KeyValuePair? removedPair;
        if (!bucket.TryAcquire(timeout))
            throw new TimeoutException();
        try
        {
            removedPair = bucket.TryRemove(keyComparerCopy, key, hashCode);
        }
        finally
        {
            bucket.Release();
        }

        if (removedPair is null)
        {
            return false;
        }

        if (removedPair.ReleaseCounter() is false)
        {
            Eviction?.Invoke(key, GetValue(removedPair));
            ClearValue(removedPair);
        }

        return true;
    }

    /// <summary>
    /// Invalidates the entire cache.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public async ValueTask InvalidateAsync(CancellationToken token = default)
    {
        var cleanup = CreateCleanupAction(Eviction);
        var cts = token.LinkTo(lifetimeToken);

        try
        {
            foreach (var bucket in buckets)
            {
                var lockTaken = false;
                try
                {
                    await bucket.AcquireAsync(token).ConfigureAwait(false);
                    lockTaken = true;

                    bucket.Invalidate(keyComparer, cleanup);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
                {
                    throw cts.CancellationOrigin == lifetimeToken
                        ? new ObjectDisposedException(GetType().Name)
                        : new OperationCanceledException(cts.CancellationOrigin);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
                {
                    throw new ObjectDisposedException(ObjectName);
                }
                finally
                {
                    if (lockTaken)
                        bucket.Release();
                }
            }
        }
        finally
        {
            cts?.Dispose();
        }

        static Action<KeyValuePair> CreateCleanupAction(Action<TKey, TValue>? eviction)
        {
            return eviction is not null
                ? pair => CleanUp(eviction, pair)
                : ClearValue;
        }

        static void CleanUp(Action<TKey, TValue> eviction, KeyValuePair removedPair)
        {
            eviction(removedPair.Key, GetValue(removedPair));
            ClearValue(removedPair);
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
                completionSource.Dispose();
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
    /// Represents a session that can be used to read the cache record value.
    /// </summary>
    /// <remarks>
    /// While session alive, the record cannot be evicted.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadSession : IDisposable
    {
        private readonly Action<TKey, TValue>? eviction;
        private readonly KeyValuePair valueHolder;

        internal ReadSession(Action<TKey, TValue>? eviction, KeyValuePair valueHolder)
        {
            this.eviction = eviction;
            this.valueHolder = valueHolder;
        }

        internal bool IsValid => valueHolder is not null;

        /// <summary>
        /// Gets the value of the cache record.
        /// </summary>
        public TValue Value => GetValue(valueHolder);

        /// <summary>
        /// Closes the session.
        /// </summary>
        void IDisposable.Dispose()
        {
            if (valueHolder?.ReleaseCounter() is false)
            {
                eviction?.Invoke(valueHolder.Key, GetValue(valueHolder));
                ClearValue(valueHolder);
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
    public readonly struct ReadOrWriteSession : IDisposable
    {
        private readonly RandomAccessCache<TKey, TValue> cache;
        private readonly object bucketOrValueHolder; // Bucket or KeyValuePair
        private readonly TKey key;
        private readonly int hashCode;

        internal ReadOrWriteSession(RandomAccessCache<TKey, TValue> cache, Bucket bucket, TKey key, int hashCode)
        {
            this.cache = cache;
            bucketOrValueHolder = bucket;
            this.key = key;
            this.hashCode = hashCode;
        }

        internal ReadOrWriteSession(RandomAccessCache<TKey, TValue> cache, KeyValuePair valueHolder)
        {
            this.cache = cache;
            bucketOrValueHolder = valueHolder;
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
            if (bucketOrValueHolder is KeyValuePair valueHolder)
            {
                result = GetValue(valueHolder);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Promotes or modifies the cache record value.
        /// </summary>
        /// <param name="value">The value to promote or replace the existing value.</param>
        /// <exception cref="InvalidOperationException">The session is invalid; the value promotes more than once.</exception>
        public void SetValue(TValue value)
        {
            switch (bucketOrValueHolder)
            {
                case Bucket bucket when bucket.TryAdd(key, hashCode, value) is { } newPair:
                    cache.Promote(newPair);
                    break;
                case KeyValuePair existingPair:
                    RandomAccessCache<TKey, TValue>.SetValue(existingPair, value);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Closes the session.
        /// </summary>
        void IDisposable.Dispose()
        {
            switch (bucketOrValueHolder)
            {
                case Bucket bucket:
                    bucket.MarkAsReadyToAdd();
                    bucket.Release();
                    break;
                case KeyValuePair pair when pair.ReleaseCounter() is false:
                    cache.Eviction?.Invoke(key, GetValue(pair));
                    ClearValue(pair);
                    break;
            }
        }
    }
}