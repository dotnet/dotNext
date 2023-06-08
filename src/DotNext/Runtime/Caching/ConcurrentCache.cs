using System.Collections;
using System.Runtime.CompilerServices;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents concurrent cache.
/// </summary>
/// <remarks>
/// The cache provides O(1) lookup performance if there is no hash collision. Asymptotic complexity of other
/// operations are same as for <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> class.
/// The cache has the following architecture to deal with lock contention: the access to the concurrent dictionary is
/// synchronous while the access to the eviction deque is asynchronous. All actions need to be applied to the deque
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
    private static readonly bool IsValueWriteAtomic;

    static ConcurrentCache()
    {
        var valueType = typeof(TValue);

        IsValueWriteAtomic = Type.GetTypeCode(valueType) switch
        {
            TypeCode.SByte or TypeCode.Byte
                or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Single or TypeCode.Char or TypeCode.Boolean
                or TypeCode.String or TypeCode.DBNull => true,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => IntPtr.Size is sizeof(long),
            _ => valueType.IsOneOf(typeof(nint), typeof(nuint)) || Unsafe.SizeOf<TValue>() == IntPtr.Size && RuntimeHelpers.IsReferenceOrContainsReferences<TValue>(),
        };
    }

    private readonly int concurrencyLevel;
    private unsafe readonly delegate*<KeyValuePair, Command> addOrReadCommand;

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

        buckets = new KeyValuePair?[capacity];
        Span.Initialize<object>(locks = new object[capacity]);
        this.keyComparer = keyComparer;
        this.concurrencyLevel = concurrencyLevel;
        unsafe
        {
            addOrReadCommand = evictionPolicy switch
            {
                CacheEvictionPolicy.LRU => &OnAddOrReadLRU,
                CacheEvictionPolicy.LFU => &OnAddOrReadLFU,
                _ => throw new ArgumentOutOfRangeException(nameof(evictionPolicy)),
            };
        }

        commandQueueReadPosition = commandQueueWritePosition = new();

        static Command OnAddOrReadLFU(KeyValuePair target) => new AddOrReadLFUCommand(target);

        static Command OnAddOrReadLRU(KeyValuePair target) => new AddOrReadLRUCommand(target);
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
    /// Gets the capacity of this cache.
    /// </summary>
    public int Capacity => buckets.Length;

    /// <summary>
    /// Gets enumerator over all key/value pairs.
    /// </summary>
    /// <returns>Unsorted enumerator over all key/value pairs.</returns>
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

    /// <summary>
    /// Gets a sorted set of cache entries.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="GetEnumerator"/>, this method allows to obtain sorted set of cache entries.
    /// However, the method has impact performance on the overall cache. It suspends eviction process during execution.
    /// </remarks>
    /// <param name="buffer">The buffer used as a destination to write cache entries.</param>
    /// <param name="descendingOrder">
    /// <see langword="true"/> to start from the least (or most) recently used cache entry;
    /// <see langword="false"/> to start from the eldest used cache entry.
    /// </param>
    /// <returns>The actual number of written items.</returns>
    public int TakeSnapshot(Span<KeyValuePair<TKey, TValue>> buffer, bool descendingOrder = true)
    {
        var count = 0;
        lock (evictionLock)
        {
            if (descendingOrder)
            {
                for (var current = firstPair; current is not null && count < buffer.Length; current = current.Links.Next)
                {
                    Unsafe.Add(ref MemoryMarshal.GetReference(buffer), count++) = new(current.Key, GetValue(current));
                }
            }
            else
            {
                for (var current = lastPair; current is not null && count < buffer.Length; current = current.Links.Previous)
                {
                    Unsafe.Add(ref MemoryMarshal.GetReference(buffer), count++) = new(current.Key, GetValue(current));
                }
            }
        }

        return count;
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}