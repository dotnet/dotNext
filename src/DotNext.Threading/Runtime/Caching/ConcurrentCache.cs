using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents concurrect cache.
/// </summary>
/// <typeparam name="TKey">The key of the cache entry.</typeparam>
/// <typeparam name="TValue">The cached value.</typeparam>
public partial class ConcurrentCache<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
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

    private Action<TKey, TValue>? evictionHandler;

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
    public ConcurrentCache(int capacity, int concurrencyLevel, CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.LRU, IEqualityComparer<TKey>? keyComparer = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        if (concurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        if (Enum.GetName(evictionPolicy) is null)
            throw new ArgumentOutOfRangeException(nameof(evictionPolicy));

        table = new(capacity, keyComparer);
        this.concurrencyLevel = concurrencyLevel;
        commandQueueWritePosition = new(CommandType.Read, new SentinelKeyValuePair(concurrencyLevel));

        // construct thread-bounded buffers
        var commandReader = new CommandQueueReader(commandQueueWritePosition);
        Func<int, CommandQueueReader, Table, EvictionDeque> dequeFactory = evictionPolicy switch
        {
            CacheEvictionPolicy.LFU => static (index, reader, table) => new LFUEvictionStrategy(index, reader, table),
            _ => static (index, reader, table) => new LRUEvictionStrategy(index, reader, table),
        };

        EvictionDeque last = currentDeque = dequeFactory(0, commandReader, table);

        for (var index = 1; index < concurrencyLevel; index++)
        {
            var deque = dequeFactory(index, commandReader, table);
            last.Next = deque;
            last = deque;
        }

        last.Next = currentDeque;
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
    public ConcurrentCache(int capacity, CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.LRU, IEqualityComparer<TKey>? keyComparer = null)
        : this(capacity, RecommendedCapacity, evictionPolicy, keyComparer)
    {
    }

    private static int RecommendedCapacity
    {
        get
        {
            var result = Environment.ProcessorCount;
            return result + ((result + 1) / 2);
        }
    }

    /// <summary>
    /// Gets or sets cache entry.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <returns>The cache entry.</returns>
    /// <exception cref="KeyNotFoundException">The cache entry with <paramref name="key"/> doesn't exist.</exception>
    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set => TryAdd(key, value, true, out _);
    }

    /// <summary>
    /// Adds a new cache entry if the cache is not full.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="value">The cache entry.</param>
    /// <returns><see langword="true"/> if the entry is added successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TKey key, TValue value) => TryAdd(key, value, false, out _);

    /// <summary>
    /// Adds or modifies the cache entry as an atomic operation.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="value">The cache entry.</param>
    /// <param name="added">
    /// <see langword="true"/> if a new entry is added;
    /// <see langword="false"/> if the existing entry is modified.
    /// </param>
    /// <returns>
    /// <paramref name="value"/> if <paramref name="added"/> is <see langword="true"/>;
    /// or the value before modification.
    /// </returns>
    public TValue AddOrUpdate(TKey key, TValue value, out bool added)
    {
        if (added = TryAdd(key, value, true, out var result))
            result = value;

        return result!;
    }

    /// <summary>
    /// Gets or adds the cache entry as an atomic operation.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="value">The cache entry.</param>
    /// <param name="added">
    /// <see langword="true"/> if a new entry is added;
    /// <see langword="false"/> if the entry is already exist.
    /// </param>
    /// <returns>
    /// <paramref name="value"/> if <paramref name="added"/> is <see langword="true"/>;
    /// or existing value.
    /// </returns>
    public TValue GetOrAdd(TKey key, TValue value, out bool added)
    {
        TValue? result;
        if (TryGetValue(key, out result))
        {
            added = false;
        }
        else if (added = TryAdd(key, value, false, out result))
        {
            result = value;
        }

        return result!;
    }

    /// <summary>
    /// Attempts to get existing cache entry.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="value">The cache entry, if successful.</param>
    /// <returns><see langword="true"/> if the cache entry exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var keyComparer = table.KeyComparer;
        int hashCode;
        KeyValuePair? pair;

        if (keyComparer is null)
        {
            hashCode = key.GetHashCode();
            for (pair = Volatile.Read(ref table.GetBucket(hashCode)); pair is not null; pair = pair.Next)
            {
                if (hashCode == pair.KeyHashCode && (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, pair.Key) : pair.Key.Equals(key)))
                {
                    if (!pair.IsAlive.Value)
                        break;

                    EnqueueCommandAndDrainQueue(CommandType.Read, pair);
                    value = pair.Value;
                    return true;
                }
            }
        }
        else
        {
            hashCode = keyComparer.GetHashCode(key);
            for (pair = Volatile.Read(ref table.GetBucket(hashCode)); pair is not null; pair = pair.Next)
            {
                if (hashCode == pair.KeyHashCode && keyComparer.Equals(key, pair.Key))
                {
                    if (!pair.IsAlive.Value)
                        break;

                    EnqueueCommandAndDrainQueue(CommandType.Read, pair);
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to remove the cache entry.
    /// </summary>
    /// <remarks>
    /// This method will not raise <see cref="OnEvicted"/> event for the removed entry.
    /// </remarks>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="value">The cache entry, if successful.</param>
    /// <returns><see langword="true"/> if the cache entry removed successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var keyComparer = table.KeyComparer;
        var hashCode = keyComparer?.GetHashCode() ?? key.GetHashCode();
        ref var bucket = ref table.GetBucket(hashCode, out var bucketLock);
        bool result;
        KeyValuePair pair;

        lock (bucketLock)
        {
            if (keyComparer is null)
            {
                for (KeyValuePair? current = Volatile.Read(ref bucket), previous = null; current is not null; previous = current, current = current.Next)
                {
                    if (hashCode == current.KeyHashCode && (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, current.Key) : current.Key.Equals(key)))
                    {
                        if (current.IsAlive.TrueToFalse())
                        {
                            result = true;
                            pair = current;
                            if (previous is null)
                                Volatile.Write(ref bucket, current.Next);
                            else
                                previous.Next = current.Next;

                            table.OnRemoved();
                            value = current.Value;
                            goto enqueue_and_exit;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                for (KeyValuePair? current = Volatile.Read(ref bucket), previous = null; current is not null; previous = current, current = current.Next)
                {
                    if (hashCode == current.KeyHashCode && keyComparer.Equals(key, current.Key))
                    {
                        if (current.IsAlive.TrueToFalse())
                        {
                            result = true;
                            pair = current;
                            if (previous is null)
                                Volatile.Write(ref bucket, current.Next);
                            else
                                previous.Next = current.Next;

                            table.OnRemoved();
                            value = current.Value;
                            goto enqueue_and_exit;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            value = default;
            result = false;
            goto exit;
        }

    enqueue_and_exit:
        EnqueueCommandAndDrainQueue(CommandType.Remove, pair);

    exit:
        return result;
    }

    /// <summary>
    /// Gets or sets a handler that can be used to capture evicted cache items.
    /// </summary>
    public event Action<TKey, TValue> OnEvicted
    {
        add => evictionHandler += value;
        remove => evictionHandler -= value;
    }

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    public void Clear()
    {
        var acquiredLocks = 0;

        try
        {
            // block all keys
            acquiredLocks = table.AcquireAllLocks();

            // block all deques
            EvictionDeque firstDeque = this.currentDeque, currentDeque = firstDeque;
            do
            {
                Monitor.Enter(currentDeque);
                currentDeque = currentDeque.Next;
            }
            while (!ReferenceEquals(currentDeque, firstDeque));

            // mark all pairs as removed
            table.Clear();

            // clear deques
            currentDeque = firstDeque;

            do
            {
                currentDeque.Clear();
                currentDeque = currentDeque.Next;
            }
            while (!ReferenceEquals(currentDeque, firstDeque));

            // release deques
            currentDeque = firstDeque;
            do
            {
                Monitor.Exit(currentDeque);
                currentDeque = currentDeque.Next;
            }
            while (!ReferenceEquals(currentDeque, firstDeque));
        }
        finally
        {
            table.ReleaseLocks(acquiredLocks);
        }
    }

    /// <inheritdoc/>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => table.Keys;

    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => table.Values;

    /// <inheritdoc />
    bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>
    /// Gets the number of cache entries.
    /// </summary>
    public int Count => table.Count;

    /// <summary>
    /// Gets enumerator over all key/value pairs.
    /// </summary>
    /// <returns>Gets the enumerator over all key/value pairs.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => table.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}