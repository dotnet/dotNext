using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Runtime.Caching;

using Diagnostics.Metrics;
using static RandomAccessCache;

public sealed class UnmanagedRandomAccessCacheTests : Test
{
    private static ReadOnlyMemory<byte> Key(int key) => BitConverter.GetBytes(key);

    private static long Value(int key) => key * 31L;

    private static async Task<bool> InsertAsync(RandomAccessCache<long> cache, int key)
    {
        await using var session = await cache.ChangeAsync(AsKey(Key(key)), TestToken);
        if (session.TryGetValue(out _))
            return false;

        session.SetValue(Value(key));
        return true;
    }

    [Fact]
    public static async Task InsertAndRead()
    {
        await using var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 42);

        True(cache.TryRead(Key(42).Span, out var session));
        using (session)
        {
            Equal(Value(42), session.Value);
            Equal(Key(42).Span, session.Key);
        }

        False(cache.TryRead(Key(43).Span, out _));
        True(cache.Contains(Key(42).Span));
    }

    [Fact]
    public static async Task ExistingEntry()
    {
        await using var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 1);

        await using var session = await cache.ChangeAsync(AsKey(Key(1)), TestToken);
        True(session.TryGetValue(out var value));
        Equal(Value(1), value);
        Throws<InvalidOperationException>(() => session.SetValue(0L));
    }

    [Fact]
    public static async Task InsertTwice()
    {
        await using var cache = new RandomAccessCache<long>(16);
        await using var session = await cache.ChangeAsync(AsKey(Key(1)), TestToken);
        session.SetValue(1L);
        Throws<InvalidOperationException>(() => session.SetValue(2L));
    }

    [Fact]
    public static async Task Invalidation()
    {
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(16) { MeasurementTags = metrics.Tags };
        await InsertAsync(cache, 1);

        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));
        False(cache.TryRead(Key(1).Span, out _));
        False(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task EvictionOnOverflow()
    {
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(16) { MeasurementTags = metrics.Tags };
        for (var i = 0; i < 1000; i++)
            await InsertAsync(cache, i);

        var count = 0;
        for (var i = 0; i < 1000; i++)
            count += cache.Contains(Key(i).Span) ? 1 : 0;

        InRange(count, 1, cache.Capacity);

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task SessionOutlivesCache()
    {
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(16) { MeasurementTags = metrics.Tags };
        await InsertAsync(cache, 1);

        True(cache.TryRead(Key(1).Span, out var session));
        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));
        await cache.DisposeAsync();

        // the session keeps the entry alive
        Equal(Value(1), session.ValueRef);
        Equal(1L, metrics.Entries);

        session.Dispose();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task DisposedCacheAccess()
    {
        var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 1);
        await cache.DisposeAsync();

        False(cache.TryRead(Key(1).Span, out _));
        await ThrowsAsync<ObjectDisposedException>(cache.ChangeAsync(AsKey(Key(1)), TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(AsKey(Key(1)), TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.TryRemoveAsync(AsKey(Key(1)), TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.ReplaceAsync(AsKey(Key(1)), TestToken).AsTask);
    }

    [Fact]
    public static async Task LockTimeout()
    {
        await using var cache = new RandomAccessCache<long>(16);

        // the free bucket can be acquired with zero timeout
        using (cache.Change(AsKey(Key(1)), TimeSpan.Zero, TestToken))
        {
        }

        // the session holds the bucket lock
        var session = await cache.ChangeAsync(AsKey(Key(1)), TestToken);
        Throws<TimeoutException>(() => cache.Change(AsKey(Key(1)), TimeSpan.FromMilliseconds(50), TestToken));
        Throws<TimeoutException>(() => cache.Invalidate(AsKey(Key(1)), TimeSpan.FromMilliseconds(50), TestToken));

        session.SetValue(1L);
        await session.DisposeAsync();

        True(cache.Invalidate(AsKey(Key(1)), DefaultTimeout, TestToken));
        Throws<ArgumentOutOfRangeException>(() => cache.Change(AsKey(Key(1)), TimeSpan.FromMilliseconds(-2), TestToken));
    }

    [Fact]
    public static async Task EvictionCallback()
    {
        const int count = 100;
        var evicted = new ConcurrentDictionary<int, long>();
        var duplicates = 0;
        var cache = new RandomAccessCache<long>(16)
        {
            Eviction = (key, value) =>
            {
                if (!evicted.TryAdd(BitConverter.ToInt32(key), value))
                    Interlocked.Increment(ref duplicates);
            },
        };

        for (var i = 0; i < count; i++)
            True(await InsertAsync(cache, i));

        // the overflow evicts the older records
        InRange(evicted.Count, count - cache.Capacity, count - 1);

        // the open session delays the callback
        var key = Enumerable.Range(0, count).First(k => cache.Contains(Key(k).Span));
        True(cache.TryRead(Key(key).Span, out var session));
        True(await cache.InvalidateAsync(AsKey(Key(key)), TestToken));
        await cache.DisposeAsync();
        False(evicted.ContainsKey(key));

        session.Dispose();
        True(evicted.ContainsKey(key));

        // every record is reported once with its value, the remaining ones on disposal
        Equal(count, evicted.Count);
        Equal(0, duplicates);
        All(evicted, static pair => Equal(Value(pair.Key), pair.Value));
    }

    [Fact]
    public static async Task InvalidationKeepsLiveRecords()
    {
        await using var cache = new RandomAccessCache<long>(100);
        var capacity = cache.Capacity;
        for (var i = 0; i < capacity; i++)
            True(await InsertAsync(cache, i));

        // invalidate the newer half
        var half = capacity / 2;
        for (var i = half; i < capacity; i++)
            True(await cache.InvalidateAsync(AsKey(Key(i)), TestToken));

        // the invalidated records don't occupy the cache, so nothing has to be evicted
        for (var i = capacity; i < 2 * capacity - half; i++)
            True(await InsertAsync(cache, i));

        All(Enumerable.Range(0, half), key => True(cache.Contains(Key(key).Span)));
    }

    [Fact]
    public static async Task InvalidateSieveHand()
    {
        var evicted = new ConcurrentQueue<int>();
        await using var cache = new RandomAccessCache<long>(16)
        {
            Eviction = (key, _) => evicted.Enqueue(BitConverter.ToInt32(key)),
        };

        var capacity = cache.Capacity;
        for (var i = 0; i < capacity; i++)
            True(await InsertAsync(cache, i));

        // the hand evicts the oldest record and stops at the next one
        True(await InsertAsync(cache, capacity));
        Equal([0], evicted);

        // the record under the hand is removed, the hand moves to the next oldest record
        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));
        True(await InsertAsync(cache, capacity + 1));
        True(await InsertAsync(cache, capacity + 2));

        Equal([0, 1, 2], evicted);
        All(Enumerable.Range(3, capacity), key => True(cache.Contains(Key(key).Span)));
    }

    [Fact]
    public static async Task InvalidatedRecordIsReclaimed()
    {
        var evicted = new ConcurrentQueue<int>();
        await using var cache = new RandomAccessCache<long>(16)
        {
            Eviction = (key, _) => evicted.Enqueue(BitConverter.ToInt32(key)),
        };

        True(await InsertAsync(cache, 1));
        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));

        // the promotion of the next record guarantees that the signal is processed
        True(await InsertAsync(cache, 2));
        Equal([1], evicted);
    }

    [Fact]
    public static async Task TryRemove()
    {
        using var metrics = new CacheMetrics();
        var evicted = new ConcurrentQueue<int>();
        var cache = new RandomAccessCache<long>(16)
        {
            MeasurementTags = metrics.Tags,
            Eviction = (key, _) => evicted.Enqueue(BitConverter.ToInt32(key)),
        };

        True(await InsertAsync(cache, 1));

        var result = await cache.TryRemoveAsync(AsKey(Key(1)), TestToken);
        True(result.HasValue);
        using (var session = result.GetValueOrDefault())
        {
            Equal(Value(1), session.Value);
            Equal(Key(1).Span, session.Key);

            False(cache.Contains(Key(1).Span));
            False(cache.TryRead(Key(1).Span, out _));
            Null(await cache.TryRemoveAsync(AsKey(Key(1)), TestToken));

            // the promotion of the next record guarantees that the signal is processed,
            // but the session keeps the removed record alive
            True(await InsertAsync(cache, 2));
            Empty(evicted);
            Equal(Value(1), session.Value);
        }

        Equal([1], evicted);

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task TryRemoveSynchronously()
    {
        await using var cache = new RandomAccessCache<long>(16);
        True(await InsertAsync(cache, 1));

        True(cache.TryRemove(AsKey(Key(1)), out var session, DefaultTimeout, TestToken));
        using (session)
        {
            Equal(Value(1), session.Value);
        }

        False(cache.TryRemove(AsKey(Key(1)), out _, DefaultTimeout, TestToken));
        False(cache.Contains(Key(1).Span));
    }

    [Fact]
    public static async Task RemovedRecordOutlivesCache()
    {
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(16) { MeasurementTags = metrics.Tags };
        True(await InsertAsync(cache, 1));

        var session = (await cache.TryRemoveAsync(AsKey(Key(1)), TestToken)).GetValueOrDefault();
        await cache.DisposeAsync();

        // the session keeps the entry alive
        Equal(Value(1), session.Value);
        Equal(1L, metrics.Entries);

        session.Dispose();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task Replace()
    {
        using var metrics = new CacheMetrics();
        var evicted = new ConcurrentQueue<long>();
        var cache = new RandomAccessCache<long>(16)
        {
            MeasurementTags = metrics.Tags,
            Eviction = (_, value) => evicted.Enqueue(value),
        };

        True(await InsertAsync(cache, 1));
        True(cache.TryRead(Key(1).Span, out var oldSession));

        await using (var session = await cache.ReplaceAsync(AsKey(Key(1)), TestToken))
        {
            False(session.TryGetValue(out _));
            False(cache.Contains(Key(1).Span));
            session.SetValue(42L);
        }

        True(cache.TryRead(Key(1).Span, out var newSession));
        using (newSession)
        {
            Equal(42L, newSession.Value);
        }

        // the session keeps the replaced record alive, the promotion of the new record guarantees that the signal is processed
        Empty(evicted);
        Equal(Value(1), oldSession.Value);
        oldSession.Dispose();
        Equal([Value(1)], evicted);

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task ReplaceMissingRecord()
    {
        await using var cache = new RandomAccessCache<long>(16);

        using (var session = cache.Replace(AsKey(Key(1)), DefaultTimeout, TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue(42L);
        }

        True(cache.TryRead(Key(1).Span, out var readSession));
        using (readSession)
        {
            Equal(42L, readSession.Value);
        }
    }

    [Fact]
    public static async Task ReplaceWithoutValue()
    {
        await using var cache = new RandomAccessCache<long>(16);
        True(await InsertAsync(cache, 1));

        // the session is closed without the value, so the record is just removed
        await using (await cache.ReplaceAsync(AsKey(Key(1)), TestToken))
        {
        }

        False(cache.Contains(Key(1).Span));
        True(await InsertAsync(cache, 1));
        True(cache.Contains(Key(1).Span));
    }

    [Fact]
    public static async Task InvalidateAll()
    {
        using var metrics = new CacheMetrics();
        var evicted = new ConcurrentQueue<int>();
        var cache = new RandomAccessCache<long>(16)
        {
            MeasurementTags = metrics.Tags,
            Eviction = (key, _) => evicted.Enqueue(BitConverter.ToInt32(key)),
        };

        var capacity = cache.Capacity;
        for (var i = 0; i < capacity; i++)
            True(await InsertAsync(cache, i));

        await cache.InvalidateAsync(TestToken);
        All(Enumerable.Range(0, capacity), key => False(cache.Contains(Key(key).Span)));

        // the promotion of the next record guarantees that the signal is processed
        True(await InsertAsync(cache, capacity));
        Equal(Enumerable.Range(0, capacity), evicted.Order());

        // the invalidated records don't occupy the cache, so nothing has to be evicted
        for (var i = capacity + 1; i < 2 * capacity; i++)
            True(await InsertAsync(cache, i));

        Equal(capacity, evicted.Count);
        All(Enumerable.Range(capacity, capacity), key => True(cache.Contains(Key(key).Span)));

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task InvalidateAllWaitsForSession()
    {
        await using var cache = new RandomAccessCache<long>(16);
        True(await InsertAsync(cache, 1));

        // the session holds the bucket lock
        var session = await cache.ChangeAsync(AsKey(Key(2)), TestToken);
        var task = cache.InvalidateAsync(TestToken).AsTask();
        False(task.IsCompleted);

        session.SetValue(Value(2));
        await session.DisposeAsync();
        await task.WaitAsync(DefaultTimeout, TestToken);

        False(cache.Contains(Key(1).Span));
        False(cache.Contains(Key(2).Span));
    }

    [Fact]
    public static async Task InvalidateAllCanceled()
    {
        await using var cache = new RandomAccessCache<long>(16);
        True(await InsertAsync(cache, 1));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        var session = await cache.ChangeAsync(AsKey(Key(2)), TestToken);
        var task = cache.InvalidateAsync(cts.Token).AsTask();
        await cts.CancelAsync();
        await ThrowsAnyAsync<OperationCanceledException>(() => task);
        await session.DisposeAsync();

        // the acquired locks are released
        True(cache.Contains(Key(1).Span));
        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));
        await cache.InvalidateAsync(TestToken);
    }

    [Fact]
    public static async Task FinalizerReleasesMemory()
    {
        using var metrics = new CacheMetrics();
        await CreateAbandonedCacheAsync(metrics.Tags);

        // the eviction loop can still run on the thread pool, its stack keeps the cache reachable
        for (var elapsed = Stopwatch.StartNew(); metrics.Entries is not 0L || metrics.Memory is not 0L; await Task.Delay(10, TestToken))
        {
            True(elapsed.Elapsed < DefaultTimeout);

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task CreateAbandonedCacheAsync(TagList tags)
        {
            var cache = new RandomAccessCache<long>(16) { MeasurementTags = tags };
            for (var i = 0; i < 100; i++)
                await InsertAsync(cache, i);

            // the invalidated entry is retired to the epoch, the session is never closed
            True(await cache.InvalidateAsync(AsKey(Key(99)), TestToken));
            True(cache.TryRead(Key(98).Span, out _));
        }
    }

    [Fact]
    public static async Task Metrics()
    {
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(16) { MeasurementTags = metrics.Tags };

        // the bucket array is allocated in the managed heap
        Equal(0L, metrics.Memory);
        Equal(0L, metrics.Entries);

        True(await InsertAsync(cache, 1));
        True(await InsertAsync(cache, 2));
        Equal(2L, metrics.Entries);

        // the header with the value, and the key
        var entryMemory = metrics.Memory / 2L;
        True(entryMemory >= sizeof(long) + sizeof(int));

        // the invalidated entry is allocated until the epoch reclaims it
        True(await cache.InvalidateAsync(AsKey(Key(1)), TestToken));
        True(await InsertAsync(cache, 3));
        for (var elapsed = Stopwatch.StartNew(); metrics.Entries is not 2L; await Task.Delay(10, TestToken))
        {
            True(elapsed.Elapsed < DefaultTimeout);
        }

        await cache.DisposeAsync();
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
    }

    [Fact]
    public static async Task StressTest()
    {
        const int keys = 512;
        const int iterations = 20_000;

        // a small capacity causes constant eviction and reclamation
        var insertions = 0;
        var evictions = 0;
        using var metrics = new CacheMetrics();
        var cache = new RandomAccessCache<long>(64, concurrencyLevel: 4)
        {
            MeasurementTags = metrics.Tags,
            Eviction = (key, value) =>
            {
                Equal(Value(BitConverter.ToInt32(key)), value);
                Interlocked.Increment(ref evictions);
            },
        };
        var workers = new Task[Environment.ProcessorCount * 2];
        for (var i = 0; i < workers.Length; i++)
            workers[i] = Task.Run(() => WorkerAsync(Random.Shared.Next()), TestToken);

        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(2), TestToken);

        // the evicted and invalidated entries are reclaimed without disposal:
        // the SIEVE list holds at most Capacity entries, the queue commands are managed objects
        if (await InsertAsync(cache, keys))
            Interlocked.Increment(ref insertions);

        var elapsed = Stopwatch.StartNew();
        while (metrics.Entries > cache.Capacity)
        {
            True(elapsed.Elapsed < DefaultTimeout);
            await Task.Delay(10, TestToken);
        }

        await cache.DisposeAsync().AsTask().WaitAsync(DefaultTimeout, TestToken);
        Equal(0L, metrics.Entries);
        Equal(0L, metrics.Memory);
        Equal(Volatile.Read(in insertions), Volatile.Read(in evictions));

        async Task WorkerAsync(int seed)
        {
            var random = new Random(seed);
            for (var i = 0; i < iterations; i++)
            {
                var key = random.Next(keys);
                switch (random.Next(4))
                {
                    case 0:
                        if (await InsertAsync(cache, key))
                            Interlocked.Increment(ref insertions);

                        break;
                    case 1:
                        await cache.InvalidateAsync(AsKey(Key(key)), TestToken);
                        break;
                    default:
                        if (cache.TryRead(Key(key).Span, out var session))
                        {
                            using (session)
                            {
                                // a reclaimed or reused entry would expose another key or value
                                Equal(Key(key).Span, session.Key);
                                Equal(Value(key), session.Value);
                            }
                        }

                        break;
                }
            }
        }
    }

    [Fact]
    public static void StringAsSpan()
    {
        const string key = "Hello, world!";
        Equal(Encoding.Unicode.GetBytes(key), AsKey(key));
    }
}

// Aggregates the measurements of the cache tagged with the unique identifier
file sealed class CacheMetrics : IDisposable
{
    private const string TestIdAttribute = "test.id";

    private readonly string id = Guid.NewGuid().ToString();
    private readonly UpDownCounterObserver<long> entries, memory;
    private readonly MeterListener listener;

    public CacheMetrics()
    {
        entries = new(IsTagged);
        memory = new(IsTagged);
        listener = new MeterListenerBuilder()
            .Observe(static instrument => IsCacheInstrument(instrument, "entries-count"), entries)
            .Observe(static instrument => IsCacheInstrument(instrument, "allocated-memory"), memory)
            .Build();

        listener.Start();
    }

    private static bool IsCacheInstrument(Instrument instrument, string name)
        => instrument.Meter.Name is "DotNext.Runtime.Caching.RandomAccessCache.Unmanaged" && instrument.Name == name;

    private bool IsTagged(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        foreach (var (key, value) in tags)
        {
            if (key is TestIdAttribute)
                return Equals(value, id);
        }

        return false;
    }

    public TagList Tags => new() { { TestIdAttribute, id } };

    public long Entries => entries.Value;

    public long Memory => memory.Value;

    public void Dispose() => listener.Dispose();
}
