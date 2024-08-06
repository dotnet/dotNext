using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using DotNext.Threading;

namespace DotNext.Runtime.Caching;

using Numerics;

public partial class RandomAccessCache<TKey, TValue> : Disposable, IAsyncDisposable
    where TKey : notnull
    where TValue : notnull
{
    private readonly IEqualityComparer<TKey>? keyComparer;
    private readonly CancellationToken lifetimeToken;
    private readonly Task evictionTask;

    private volatile CancellationTokenSource? lifetimeSource;

    public RandomAccessCache(int cacheSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheSize);

        maxCacheSize = cacheSize;
        var dictionarySize = PrimeNumber.GetPrime(cacheSize);
        fastModMultiplier = IntPtr.Size is sizeof(ulong)
            ? PrimeNumber.GetFastModMultiplier((uint)dictionarySize)
            : default;

        buckets = new Bucket[dictionarySize];
        Span.Initialize<Bucket>(buckets);

        lifetimeSource = new();
        lifetimeToken = lifetimeSource.Token;
        promotionHead = promotionTail = new FakeKeyValuePair();
        evictionTask = DoEvictionAsync();
    }

    [DisallowNull] public Action<TKey, TValue>? OnEviction { get; init; }

    public async ValueTask<CacheEntryHandle> ModifyAsync(TKey key, CancellationToken token = default)
    {
        var keyComparerCopy = keyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? key.GetHashCode();
        var bucket = GetBucket(hashCode);

        var cts = token.LinkTo(lifetimeToken);
        try
        {
            await bucket.AcquireAsync(token).ConfigureAwait(false);
            return Modify(keyComparerCopy, bucket, key, hashCode);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
        {
            throw new OperationCanceledException(cts.CancellationOrigin);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public bool TryGet(TKey key, out ReadOnlyCacheEntryHandle scope)
    {
        var keyComparerCopy = keyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? key.GetHashCode();
        var bucket = GetBucket(hashCode);

        if (TryGet(keyComparerCopy, bucket, key, hashCode) is { } valueHolder)
        {
            scope = new(OnEviction, valueHolder);
            return true;
        }

        scope = default;
        return false;
    }

    public async ValueTask<ReadOnlyCacheEntryHandle?> TryRemoveAsync(TKey key, CancellationToken token = default)
    {
        var keyComparerCopy = keyComparer;
        var hashCode = keyComparerCopy?.GetHashCode(key) ?? key.GetHashCode();
        var bucket = GetBucket(hashCode);

        var cts = token.LinkTo(lifetimeToken);
        try
        {
            await bucket.AcquireAsync(token).ConfigureAwait(false);
            return TryRemove(keyComparerCopy, bucket, key, hashCode) is { } removedPair && removedPair.TryAcquireCounter()
                ? new ReadOnlyCacheEntryHandle(OnEviction, removedPair)
                : null;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts?.Token)
        {
            throw new OperationCanceledException(cts.CancellationOrigin);
        }
        finally
        {
            bucket.Release();
        }
    }

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
            if (disposing && Interlocked.Exchange(ref lifetimeSource, null) is { } cts)
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
        finally
        {
            base.Dispose(disposing);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyCacheEntryHandle : IDisposable
    {
        private readonly Action<TKey, TValue>? eviction;
        private readonly KeyValuePair valueHolder;

        internal ReadOnlyCacheEntryHandle(Action<TKey, TValue>? eviction, KeyValuePair valueHolder)
        {
            this.eviction = eviction;
            this.valueHolder = valueHolder;
        }

        public TValue Value => GetValue(valueHolder);

        void IDisposable.Dispose()
        {
            if (valueHolder?.ReleaseCounter() is false)
            {
                eviction?.Invoke(valueHolder.Key, GetValue(valueHolder));
                ClearValue(valueHolder);
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct CacheEntryHandle : IDisposable
    {
        private readonly RandomAccessCache<TKey, TValue> cache;
        private readonly Bucket? bucket; // null if value exists
        private readonly KeyValuePair? valueHolderOrPrevious;
        private readonly TKey key;
        private readonly int hashCode;

        internal CacheEntryHandle(RandomAccessCache<TKey, TValue> cache, Bucket bucket, KeyValuePair? previous, TKey key, int hashCode)
        {
            this.cache = cache;
            this.bucket = bucket;
            this.key = key;
            this.hashCode = hashCode;
            valueHolderOrPrevious = previous;
        }

        internal CacheEntryHandle(RandomAccessCache<TKey, TValue> cache, KeyValuePair valueHolder)
        {
            this.cache = cache;
            valueHolderOrPrevious = valueHolder;
            key = valueHolder.Key;
            hashCode = valueHolder.KeyHashCode;
        }

        public bool TryGetValue([MaybeNullWhen(false)] out TValue result)
        {
            if (bucket is null && valueHolderOrPrevious is not null)
            {
                result = GetValue(valueHolderOrPrevious);
                return true;
            }

            result = default;
            return false;
        }

        public void SetValue(TValue value)
        {
            if (bucket is not null)
            {
                // promote a new value
                var newPair = CreatePair(key!, value, hashCode);
                cache.Promote(newPair);
                ref var location = ref valueHolderOrPrevious is null ? ref bucket.First : ref valueHolderOrPrevious.NextInBucket;
                Volatile.Write(ref location, newPair);
            }
            else if (valueHolderOrPrevious is not null)
            {
                RandomAccessCache<TKey, TValue>.SetValue(valueHolderOrPrevious, value);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        void IDisposable.Dispose()
        {
            if (bucket is not null)
            {
                bucket.Release();
            }
            else if (valueHolderOrPrevious?.ReleaseCounter() is false)
            {
                cache.OnEviction?.Invoke(key, GetValue(valueHolderOrPrevious));
                ClearValue(valueHolderOrPrevious);
            }
        }
    }
}