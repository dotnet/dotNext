using System.Collections;

namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents concurrect cache.
/// </summary>
/// <remarks>
/// The cache provides O(1) lookup performance if there is no hash collision. Asymptotic complexity of other
/// operations are same as for <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> class.
/// The cache has the following architecture to deal with lock contention: the access to the concurrect dictionary is
/// synchronouse while the access to the eviction deque is asynchronous. All actions need to be applied to the deque
/// are delayed and distributed across concurrent threads. Thus, the deque is weakly consistent with the dictionary.
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the cache items.</typeparam>
public partial class ConcurrentCache<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private static readonly bool IsValueWriteAtomic;

    static ConcurrentCache()
    {
        // Section 12.6.6 of ECMA CLI explains which types can be read and written atomically without
        // the risk of tearing. See https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf
        var valueType = typeof(TValue);
        if (!valueType.IsValueType ||
            valueType == typeof(nint) ||
            valueType == typeof(nuint))
        {
            IsValueWriteAtomic = true;
        }
        else
        {
            IsValueWriteAtomic = Type.GetTypeCode(valueType) switch
            {
                TypeCode.Boolean or TypeCode.Byte or TypeCode.Char or TypeCode.Int16 or TypeCode.Int32 or TypeCode.SByte or TypeCode.Single or TypeCode.UInt16 or TypeCode.UInt32 => true,
                TypeCode.Double or TypeCode.Int64 or TypeCode.UInt64 => IntPtr.Size is sizeof(long),
                _ => false,
            };
        }
    }

    private readonly int concurrencyLevel;

    /// <summary>
    /// Initializes a new empty cache.
    /// </summary>
    /// <param name="capacity">The maximum number of cached items.</param>
    /// <param name="concurrencyLevel">The number of thread that can access the cache concurrently.</param>
    /// <param name="evictionPolicy">The eviction policy describing how the cached items must be removed on cache overflow.</param>
    /// <param name="keyComparer">The comparer that can be used to compare keys within the cache.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than 1;
    /// or <paramref name="concurrencyLevel"/> is less than 1;
    /// or <paramref name="evictionPolicy"/> is invalid.
    /// </exception>
    public ConcurrentCache(int capacity, int concurrencyLevel, CacheEvictionPolicy evictionPolicy, IEqualityComparer<TKey>? keyComparer = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        if (Enum.GetName(evictionPolicy) is null)
            throw new ArgumentOutOfRangeException(nameof(evictionPolicy));

        buckets = new KeyValuePair?[capacity];
        Span.Initialize<object>(locks = new object[capacity]);
        this.keyComparer = keyComparer;
        this.concurrencyLevel = concurrencyLevel;
        this.evictionPolicy = evictionPolicy;
        commandQueueReadPosition = commandQueueWritePosition = new();
    }

    /// <summary>
    /// Initializes a new empty cache.
    /// </summary>
    /// <param name="capacity">The maximum number of cached items.</param>
    /// <param name="evictionPolicy">The eviction policy describing how the cached items must be removed on cache overflow.</param>
    /// <param name="keyComparer">The comparer that can be used to compare keys within the cache.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than 1;
    /// or <paramref name="evictionPolicy"/> is invalid.
    /// </exception>
    public ConcurrentCache(int capacity, CacheEvictionPolicy evictionPolicy, IEqualityComparer<TKey>? keyComparer = null)
        : this(capacity, RecommendedConcurrencyLevel, evictionPolicy, keyComparer)
    {
    }

    private static int RecommendedConcurrencyLevel
    {
        get
        {
            var result = Environment.ProcessorCount;
            return result + ((result + 1) / 2);
        }
    }

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    /// <remarks>
    /// This operation locks the entire cache exclusively.
    /// </remarks>
    public void Clear()
    {
        var acquiredLocks = 0;
        var evictionLockTaken = false;

        try
        {
            // block all keys
            acquiredLocks = AcquireAllLocks();

            // block eviction queue
            Monitor.Enter(evictionLock, ref evictionLockTaken);

            // mark all pairs as removed
            RemoveAllKeys();

            // clear deque
            firstPair = lastPair = null;
        }
        finally
        {
            if (evictionLockTaken)
                Monitor.Exit(evictionLock);

            ReleaseLocks(acquiredLocks);
        }
    }

    /// <inheritdoc/>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
    {
        get
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                    yield return current.Key;
            }
        }
    }

    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
    {
        get
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                    yield return current.Value;
            }
        }
    }

    /// <inheritdoc />
    bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>
    /// Gets the number of cache entries.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}.Count"/>,
    /// this property is fast and doesn't require an exclusive lock.
    /// </remarks>
    public int Count => count;

    /// <summary>
    /// Gets enumerator over all key/value pairs.
    /// </summary>
    /// <returns>Gets the enumerator over all key/value pairs.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (var i = 0; i < buckets.Length; i++)
        {
            for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                yield return new(current.Key, current.Value);
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}