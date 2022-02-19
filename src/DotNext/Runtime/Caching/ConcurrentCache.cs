using System.Collections;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

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
    // A conforming CLI shall guarantee that read and write access to properly aligned memory
    // locations no larger than the native word size.
    // See Section I.12.6.6 in https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf
    private static bool IsValueWriteAtomic => Unsafe.SizeOf<TValue>() <= IntPtr.Size;

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
            // block eviction queue
            Monitor.Enter(evictionLock, ref evictionLockTaken);

            // block all keys
            acquiredLocks = AcquireAllLocks();

            // mark all pairs as removed
            RemoveAllKeys();

            // clear deque
            firstPair = lastPair = null;
        }
        finally
        {
            ReleaseLocks(acquiredLocks);

            if (evictionLockTaken)
                Monitor.Exit(evictionLock);
        }
    }

    /// <inheritdoc/>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
    {
        get
        {
            return GetKeys(buckets);

            static IEnumerable<TKey> GetKeys(KeyValuePair?[] buckets)
            {
                for (var i = 0; i < buckets.Length; i++)
                {
                    for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                        yield return current.Key;
                }
            }
        }
    }

    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
    {
        get
        {
            return GetValues(buckets);

            static IEnumerable<TValue> GetValues(KeyValuePair?[] buckets)
            {
                for (var i = 0; i < buckets.Length; i++)
                {
                    for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                        yield return GetValue(current);
                }
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
        return GetEnumerator(buckets);

        static IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator(KeyValuePair?[] buckets)
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                    yield return new(current.Key, GetValue(current));
            }
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}