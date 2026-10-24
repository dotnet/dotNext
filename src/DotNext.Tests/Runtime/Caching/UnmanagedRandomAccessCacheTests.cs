using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.Caching;

public sealed class UnmanagedRandomAccessCacheTests : Test
{
    private static ReadOnlyMemory<byte> Key(int key) => BitConverter.GetBytes(key);

    private static long Value(int key) => key * 31L;

    private static async Task<bool> InsertAsync(RandomAccessCache<long> cache, int key)
    {
        await using var session = await cache.ChangeAsync(Key(key), TestToken);
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

        await using var session = await cache.ChangeAsync(Key(1), TestToken);
        True(session.TryGetValue(out var value));
        Equal(Value(1), value);
        Throws<InvalidOperationException>(() => session.SetValue(0L));
    }

    [Fact]
    public static async Task InsertTwice()
    {
        await using var cache = new RandomAccessCache<long>(16);
        await using var session = await cache.ChangeAsync(Key(1), TestToken);
        session.SetValue(1L);
        Throws<InvalidOperationException>(() => session.SetValue(2L));
    }

    [Fact]
    public static async Task Invalidation()
    {
        var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 1);

        True(await cache.InvalidateAsync(Key(1), TestToken));
        False(cache.TryRead(Key(1).Span, out _));
        False(await cache.InvalidateAsync(Key(1), TestToken));

        await cache.DisposeAsync();
        Equal(0L, Accessor<long>.LiveEntries(cache));
    }

    [Fact]
    public static async Task EvictionOnOverflow()
    {
        var cache = new RandomAccessCache<long>(16);
        for (var i = 0; i < 1000; i++)
            await InsertAsync(cache, i);

        var count = 0;
        for (var i = 0; i < 1000; i++)
            count += cache.Contains(Key(i).Span) ? 1 : 0;

        InRange(count, 1, cache.Capacity);

        await cache.DisposeAsync();
        Equal(0L, Accessor<long>.LiveEntries(cache));
    }

    [Fact]
    public static async Task SessionOutlivesCache()
    {
        var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 1);

        True(cache.TryRead(Key(1).Span, out var session));
        True(await cache.InvalidateAsync(Key(1), TestToken));
        await cache.DisposeAsync();

        // the session keeps the entry alive
        Equal(Value(1), session.Value);
        Equal(1L, Accessor<long>.LiveEntries(cache));

        session.Dispose();
        Equal(0L, Accessor<long>.LiveEntries(cache));
    }

    [Fact]
    public static async Task DisposedCacheAccess()
    {
        var cache = new RandomAccessCache<long>(16);
        await InsertAsync(cache, 1);
        await cache.DisposeAsync();

        False(cache.TryRead(Key(1).Span, out _));
        await ThrowsAsync<ObjectDisposedException>(cache.ChangeAsync(Key(1), TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(Key(1), TestToken).AsTask);
    }

    [Fact]
    public static async Task LockTimeout()
    {
        await using var cache = new RandomAccessCache<long>(16);

        // the free bucket can be acquired with zero timeout
        using (cache.Change(Key(1), TimeSpan.Zero, TestToken))
        {
        }

        // the session holds the bucket lock
        var session = await cache.ChangeAsync(Key(1), TestToken);
        Throws<TimeoutException>(() => cache.Change(Key(1), TimeSpan.FromMilliseconds(50), TestToken));
        Throws<TimeoutException>(() => cache.Invalidate(Key(1), TimeSpan.FromMilliseconds(50), TestToken));

        session.SetValue(1L);
        await session.DisposeAsync();

        True(cache.Invalidate(Key(1), DefaultTimeout, TestToken));
        Throws<ArgumentOutOfRangeException>(() => cache.Change(Key(1), TimeSpan.FromMilliseconds(-2), TestToken));
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
        True(await cache.InvalidateAsync(Key(key), TestToken));
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
    public static async Task StressTest()
    {
        const int keys = 512;
        const int iterations = 20_000;

        // a small capacity causes constant eviction and reclamation
        var insertions = 0;
        var evictions = 0;
        var cache = new RandomAccessCache<long>(64, concurrencyLevel: 4)
        {
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
        // the SIEVE list holds at most Capacity entries, plus the stub at the head of the promotion queue
        if (await InsertAsync(cache, keys))
            Interlocked.Increment(ref insertions);

        var elapsed = Stopwatch.StartNew();
        while (Accessor<long>.LiveEntries(cache) > cache.Capacity + 1L)
        {
            True(elapsed.Elapsed < DefaultTimeout);
            await Task.Delay(10, TestToken);
        }

        await cache.DisposeAsync().AsTask().WaitAsync(DefaultTimeout, TestToken);
        Equal(0L, Accessor<long>.LiveEntries(cache));
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
                        await cache.InvalidateAsync(Key(key), TestToken);
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
}

file static class Accessor<TValue>
    where TValue : unmanaged
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "liveEntries")]
    private static extern ref long GetLiveEntries(RandomAccessCache<TValue> cache);

    public static long LiveEntries(RandomAccessCache<TValue> cache) => Volatile.Read(ref GetLiveEntries(cache));
}
