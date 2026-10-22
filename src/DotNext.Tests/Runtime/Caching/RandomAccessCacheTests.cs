using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNext.Runtime.Caching;

using CompilerServices;

public sealed class RandomAccessCacheTests : Test
{
    [Fact]
    public static async Task CacheOverflow()
    {
        var evictedItem = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cache = new RandomAccessCache<long, string>(15)
        {
            Eviction = (_, value) => evictedItem.TrySetResult(value),
        };

        for (long i = 0; i < 150; i++)
        {
            await using var handle = await cache.ChangeAsync(i, TestToken);
            True(Unsafe.IsNullRef(in handle.ValueRefOrNullRef));

            handle.SetValue(i.ToString());
        }

        Equal("0", await evictedItem.Task.WaitAsync(TestToken));
    }

    [Fact]
    public static async Task CacheOverflow2()
    {
        var evictedItem = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cache = new RandomAccessCache<long, string>(15)
        {
            Eviction = (_, value) => evictedItem.TrySetResult(value),
        };

        const long accessCount = 150;
        for (long i = 0; i < accessCount; i++)
        {
            var key = Random.Shared.NextInt64(accessCount);
            if (cache.TryRead(key, out var readSession))
            {
                using (readSession)
                {
                    Equal(key, readSession.Key);
                    Equal(key.ToString(), readSession.Value);
                }
            }
            else
            {
                await using var writeSession = await cache.ChangeAsync(key, TestToken);
                if (writeSession.TryGetValue(out var value))
                {
                    Equal(key.ToString(), value);
                }
                else
                {
                    writeSession.SetValue(key.ToString());
                }
            }
        }

        await evictedItem.Task;
    }
    
    [Fact]
    public static async Task StressTest()
    {
        await using var cache = new RandomAccessCache<long, string>(15);
        Null(cache.KeyComparer);

        const long accessCount = 1500;

        var task1 = RequestLoop(cache);
        var task2 = RequestLoop(cache);

        await Task.WhenAll(task1, task2);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        static async Task RequestLoop(RandomAccessCache<long, string> cache)
        {
            for (long i = 0; i < accessCount; i++)
            {
                var key = Random.Shared.NextInt64(accessCount);
                if (cache.TryRead(key, out var readSession))
                {
                    using (readSession)
                    {
                        Equal(key.ToString(), readSession.Value);
                    }
                }
                else
                {
                    using var writeSession = await cache.ChangeAsync(key);
                    if (writeSession.TryGetValue(out var value))
                    {
                        Equal(key.ToString(), value);
                    }
                    else
                    {
                        writeSession.SetValue(key.ToString());
                    }
                }
            }
        }
    }

    [Fact]
    public static async Task AddRemoveAsync()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        await using (var session = await cache.ChangeAsync(10L, TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        Null(await cache.TryRemoveAsync(11L, TestToken));
        True(cache.Contains(10L));

        using (var session = (await cache.TryRemoveAsync(10L, TestToken)).GetValueOrDefault())
        {
            Equal("10", session.Value);
        }

        False(cache.Contains(10L));
    }
    
    [Fact]
    public static void AddRemove()
    {
        using var cache = new RandomAccessCache<long, string>(15);

        using (var writeSession = cache.Change(10L, DefaultTimeout, TestToken))
        {
            False(writeSession.TryGetValue(out _));
            writeSession.SetValue("10");
        }

        False(cache.TryRemove(11L, out _, DefaultTimeout, TestToken));
        
        True(cache.Contains(10L));
        True(cache.TryRemove(10L, out var session, DefaultTimeout, TestToken));
        False(cache.Contains(10L));

        using (session)
        {
            Equal("10", session.Value);
        }

        False(cache.Contains(10L));
    }

    [Fact]
    public static async Task AddInvalidateAsync()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        using (var session = await cache.ChangeAsync(10L, TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        False(await cache.InvalidateAsync(11L, TestToken));
        True(await cache.InvalidateAsync(10L, TestToken));
    }
    
    [Fact]
    public static void AddInvalidate()
    {
        using var cache = new RandomAccessCache<long, string>(15);

        using (var session = cache.Change(10L, DefaultTimeout, TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        False(cache.Invalidate(11L, DefaultTimeout, TestToken));
        True(cache.Invalidate(10L, DefaultTimeout, TestToken));
    }

    [Fact]
    public static async Task AddTwice()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        await using var session = await cache.ChangeAsync(10L, TestToken);
        False(session.TryGetValue(out _));
        session.SetValue("10");

        Throws<InvalidOperationException>(() => session.SetValue("20"));
    }

    [Fact]
    public static async Task DisposedCacheAccess()
    {
        var cache = new RandomAccessCache<long, string>(18);
        await cache.DisposeAsync();

        await ThrowsAsync<ObjectDisposedException>(cache.ChangeAsync(0L, TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.TryRemoveAsync(0L, TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(TestToken).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(10L, TestToken).AsTask);
    }

    [Fact]
    public static async Task DisposedCacheAccess2()
    {
        using var cts = new CancellationTokenSource();
        var cache = new RandomAccessCache<long, string>(18);
        await cache.DisposeAsync();

        await ThrowsAsync<ObjectDisposedException>(cache.ChangeAsync(0L, cts.Token).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.TryRemoveAsync(0L, cts.Token).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(cts.Token).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(10L, cts.Token).AsTask);
    }

    [Fact]
    public static async Task Invalidation()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        for (long i = 0; i < 20; i++)
        {
            await using var handle = await cache.ChangeAsync(i, TestToken);
            False(handle.TryGetValue(out _));

            handle.SetValue(i.ToString());
        }

        await cache.InvalidateAsync(TestToken);
    }

    [Fact]
    public static async Task ReplaceWhileReadingAsync()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        await using (var handle = await cache.ChangeAsync(42L, TestToken))
        {
            handle.SetValue("42");
        }

        True(cache.TryRead(42L, out var readSession));

        using (readSession)
        {
            await using (var handle = await cache.ReplaceAsync(42L, TestToken))
            {
                False(handle.TryGetValue(out _));
                handle.SetValue("43");
            }

            Equal("42", readSession.Value);
        }
        
        True(cache.TryRead(42L, out readSession));

        using (readSession)
        {
            Equal("43", readSession.Value);
        }
    }
    
    [Fact]
    public static void ReplaceWhileReading()
    {
        using var cache = new RandomAccessCache<long, string>(15);

        using (var handle = cache.Change(42L, DefaultTimeout, TestToken))
        {
            handle.SetValue("42");
        }

        True(cache.TryRead(42L, out var readSession));

        using (readSession)
        {
            using (var handle = cache.Replace(42L, DefaultTimeout, TestToken))
            {
                False(handle.TryGetValue(out _));
                handle.SetValue("43");
            }

            Equal("42", readSession.Value);
        }
        
        True(cache.TryRead(42L, out readSession));

        using (readSession)
        {
            Equal("43", readSession.Value);
        }
    }

    [Fact]
    public static void EnumeratePartially()
    {
        using var cache = new RandomAccessCache<long, string>(15);
        
        using (var handle = cache.Change(42L, DefaultTimeout, TestToken))
        {
            handle.SetValue("42");
        }
        
        using (var handle = cache.Change(43L, DefaultTimeout, TestToken))
        {
            handle.SetValue("43");
        }

        using (var enumerator = cache.GetEnumerator())
        {
            True(enumerator.MoveNext());
        }
    }

    [Fact]
    public static async Task EvictLargeItemImmediately()
    {
        const long value = 101L;
        var source = new TaskCompletionSource<long>();
        await using var cache = new CacheWithWeight(42, value - 1L, 3)
        {
            Eviction = (_, v) => source.TrySetResult(v),
        };
        
        await using (var session = await cache.ChangeAsync("101", TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue(101L); // 101 > 100, must be evicted immediately
        }

        Equal(value, await source.Task.WaitAsync(TestToken));
    }

    [Fact]
    public static async Task EvictRedundantItems()
    {
        var channel = Channel.CreateBounded<long>(2);
        await using var cache = new CacheWithWeight(42, 100L, 3)
        {
            Eviction = (_, v) => True(channel.Writer.TryWrite(v)),
        };

        await using (var session = await cache.ChangeAsync("60", TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue(60L);
        }

        await using (var session = await cache.ChangeAsync("40", TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue(40L);
        }

        await using (var session = await cache.ChangeAsync("100", TestToken))
        {
            False(session.TryGetValue(out _));
            session.SetValue(100L);
        }

        var x = await channel.Reader.ReadAsync(TestToken);
        var y = await channel.Reader.ReadAsync(TestToken);
        Equal(100L, x + y);
    }

    private static async Task CheckCapacity(int initialCapacity, int threshold, Action<CacheWithWeight> assertion)
    {
        await using var cache = new CacheWithWeight(initialCapacity, 100L, threshold);
        var capacity = cache.Capacity;
        True(cache.Capacity > initialCapacity);

        for (var i = 0; i < capacity * 2; i++)
        {
            using var session = await cache.ChangeAsync(i.ToString());
            False(session.TryGetValue(out _));
            session.SetValue(i);
        }

        assertion(cache);
    }

    [Fact]
    public static async Task ResizeCache()
    {
        await CheckCapacity(2, 1, static cache => True(cache.Capacity > 6));
    }

    [Fact]
    public static async Task InfiniteThreshold()
    {
        await CheckCapacity(2, int.MaxValue, static cache => Equal(3, cache.Capacity));
    }

    [Fact]
    public static async Task TryReadUsingAlternateKey()
    {
        await using var cache = new RandomAccessCache<string, long>(15);
        await using (var handle = await cache.ChangeAsync("abc", TestToken))
        {
            False(handle.TryGetValue(out _));
            handle.SetValue(42L);
        }
        
        True(cache.TryRead<ReadOnlySpan<char>>("abc", out var readSession));
        using (readSession)
        {
            Equal(42L, readSession.Value);
        }
    }

    [Fact]
    public static async Task TryRemoveUsingAlternateKey()
    {
        await using var cache = new RandomAccessCache<Guid, long>(15)
        {
            KeyComparer = BitwiseComparer<Guid>.Instance,
        };

        var lookup = cache.GetAlternateLookup<ReadOnlyMemory<byte>>();
        var key = Guid.NewGuid();
        await using (var handle = await lookup.ChangeAsync(key.ToByteArray(), TestToken))
        {
            False(handle.TryGetValue(out _));
            handle.SetValue(42L);
        }
        
        True(lookup.TryRead(key.ToByteArray(), out var readSession));
        using (readSession)
        {
            Equal(42L, readSession.Value);
        }

        readSession = (await lookup.TryRemoveAsync(key.ToByteArray(), TestToken)).GetValueOrDefault();

        using (readSession)
        {
            Equal(42L, readSession.Value);
        }

        False(lookup.TryRead(key.ToByteArray(), out readSession));
    }
    
    [Fact]
    public static async Task InvalidateUsingAlternateKey()
    {
        await using var cache = new RandomAccessCache<Guid, long>(15)
        {
            KeyComparer = BitwiseComparer<Guid>.Instance,
        };

        var lookup = cache.GetAlternateLookup<ReadOnlyMemory<byte>>();
        var key = Guid.NewGuid();
        await using (var handle = await lookup.ReplaceAsync(key.ToByteArray(), TestToken))
        {
            False(handle.TryGetValue(out _));
            handle.SetValue(42L);
        }
        
        True(lookup.TryRead(key.ToByteArray(), out var readSession));
        using (readSession)
        {
            Equal(42L, readSession.Value);
        }

        True(await lookup.InvalidateAsync(key.ToByteArray(), TestToken));
        False(lookup.TryRead(key.ToByteArray(), out readSession));
    }

    [Fact]
    public static async Task ImportPairs()
    {
        await using var cache = new RandomAccessCache<Guid, long>(15)
        {
            KeyComparer = BitwiseComparer<Guid>.Instance,
        };

        var key = Guid.NewGuid();
        await cache.ImportAsync(new Dictionary<Guid, long> { { key, 42L }, { Guid.NewGuid(), 43L } }, TestToken);
        True(cache.TryRead(key, out var session));
        using (session)
        {
            Equal(42L, session.Value);
        }
    }

    [Fact]
    public static async Task ResizeWithInvalidatedItems()
    {
        const long maxWeight = 10L;
        await using var cache = new CacheWithWeight(2, maxWeight, 1);
        await AddAsync(cache, "a");
        await AddAsync(cache, "b");
        True(await cache.InvalidateAsync("a", TestToken));

        // no eviction, but resize happens
        var capacity = cache.Capacity;
        for (var i = 0; i < 8; i++)
            await AddAsync(cache, i.ToString());

        True(cache.Capacity > capacity);

        // eviction must see all the items inserted before the resize
        for (var i = 8; i < 50; i++)
            await AddAsync(cache, i.ToString());

        True(cache.Count() <= maxWeight);
        False(cache.Contains("b"));
        for (var i = 0; i < 8; i++)
            False(cache.Contains(i.ToString()));

        static async Task AddAsync(CacheWithWeight cache, string key)
        {
            await using var session = await cache.ChangeAsync(key, TestToken);
            False(session.TryGetValue(out _));
            session.SetValue(1L);
        }
    }

    [Fact]
    public static async Task ReadDuringResize()
    {
        var evicted = new ConcurrentDictionary<string, int>();
        await using var cache = new CacheWithWeight(2, 10L, 1)
        {
            Eviction = (key, _) => evicted.AddOrUpdate(key, 1, static (_, count) => count + 1),
        };

        await AddAsync(cache, "a");
        True(cache.TryRead("a", out var session));
        var capacity = cache.Capacity;

        // TryRead marks "a" as visited. Visit every inserted item as well, so the SIEVE hand wraps around
        // and reaches "a" again. Otherwise, the hand evicts new items only and never returns to "a"
        for (var i = 0; cache.Contains("a"); i++)
        {
            True(i < 100);
            var key = i.ToString();
            await AddAsync(cache, key);
            True(cache.TryRead(key, out var readSession));
            readSession.Dispose();
        }

        True(cache.Capacity > capacity);

        // the value must not be released while the session is alive
        False(evicted.ContainsKey("a"));
        Equal(1L, session.Value);
        session.Dispose();
        Equal(1, evicted["a"]);

        static async Task AddAsync(CacheWithWeight cache, string key)
        {
            await using var session = await cache.ChangeAsync(key, TestToken);
            False(session.TryGetValue(out _));
            session.SetValue(1L);
        }
    }

    [Fact]
    public static async Task ConcurrentResize()
    {
        await using var cache = new CacheWithWeight(2, long.MaxValue, 2);
        await Parallel.ForAsync(0, 1000, new ParallelOptions { CancellationToken = TestToken }, async (i, token) =>
        {
            var session = await cache.ChangeAsync(i.ToString(), token);
            session.SetValue(1L);

            // must not hang if the promotion is enqueued before the resize
            await session.DisposeAsync().AsTask().WaitAsync(DefaultTimeout, token);
        });
    }

    [Fact]
    public static async Task InvalidateBeforeInsertion()
    {
        await using var cache = new CacheWithStringWeight(64, long.MaxValue);
        for (var i = 0; i < 10_000; i++)
        {
            var key = i.ToString();
            using (var session = await cache.ChangeAsync(key, TestToken))
            {
                session.SetValue(key);
            }

            await cache.InvalidateAsync(key, TestToken);
        }

        // wait for the eviction queue to be processed
        await using (var session = await cache.ChangeAsync("last", TestToken))
        {
            session.SetValue("last");
        }

        False(cache.NullObserved);
        Equal("last".Length, cache.TotalWeight);
    }

    [Fact]
    public static void InvalidConcurrencyLevel()
    {
        Throws<ArgumentOutOfRangeException>(static () => new RandomAccessCache<long, string>(10, concurrencyLevel: 0));
        Throws<ArgumentOutOfRangeException>(static () => new CacheWithWeight(10, long.MaxValue, 2, concurrencyLevel: -1));
    }

    [Fact]
    public static async Task ConcurrentWritersSharingWaitQueue()
    {
        const int keys = 8;
        const int iterations = 4000;

        // the single wait queue is shared by the writers of all buckets
        await using var cache = new RandomAccessCache<int, StrongBox<int>>(64, concurrencyLevel: 1);
        var insertions = new int[keys];

        await Parallel.ForAsync(0, iterations, new ParallelOptions { CancellationToken = TestToken }, async (i, token) =>
        {
            var key = i % keys;
            await using var session = await cache.ChangeAsync(key, token);
            if (session.TryGetValue(out var counter))
            {
                Interlocked.Increment(ref counter.Value);
            }
            else
            {
                // the bucket lock is held, so the key cannot be inserted concurrently
                Interlocked.Increment(ref insertions[key]);
                await Task.Yield();
                session.SetValue(new(1));
            }
        }).WaitAsync(DefaultTimeout, TestToken);

        All(insertions, static count => Equal(1, count));

        var total = 0;
        for (var key = 0; key < keys; key++)
        {
            True(cache.TryRead(key, out var session));
            using (session)
            {
                total += session.Value.Value;
            }
        }

        Equal(iterations, total);
    }

    [Fact]
    public static async Task ConcurrentResizeSharingWaitQueue()
    {
        const int keys = 1000;
        await using var cache = new CacheWithWeight(2, long.MaxValue, 2, concurrencyLevel: 1);
        await Parallel.ForAsync(0, keys, new ParallelOptions { CancellationToken = TestToken }, async (i, token) =>
        {
            await using var session = await cache.ChangeAsync(i.ToString(), token);
            False(session.TryGetValue(out _));
            session.SetValue(1L);
        }).WaitAsync(DefaultTimeout, TestToken);

        True(cache.Capacity > 2);
        for (var i = 0; i < keys; i++)
            True(cache.Contains(i.ToString()));
    }

    [Fact]
    public static async Task ThrowingEvictionDoesNotLeakLock()
    {
        await using var cache = new RandomAccessCache<int, string>(16) { Eviction = static (_, _) => throw new ArithmeticException() };
        await using (var session = await cache.ChangeAsync(1, TestToken))
        {
            session.SetValue("a");
        }

        // the replaced entry is removed under the bucket lock, the callback throws
        await ThrowsAsync<ArithmeticException>(cache.ReplaceAsync(1, TestToken).AsTask);

        using (cache.Change(1, DefaultTimeout, TestToken))
        {
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task TimeoutDuringResize(bool replace)
    {
        await using var cache = new IntCacheWithWeight(2, collisionThreshold: 1);

        // the key is the hash code, so the key equal to the capacity is in the same bucket as 0
        var collidingKey = cache.Capacity;
        await using (var session = await cache.ChangeAsync(0, TestToken))
        {
            session.SetValue(1L);
        }

        // the lock of another bucket prevents the resize
        await using (await cache.ChangeAsync(1, TestToken))
        {
            // the first bucket requires the resize
            Throws<TimeoutException>(() => (replace
                ? cache.Replace(collidingKey, TimeSpan.FromMilliseconds(50), TestToken)
                : cache.Change(collidingKey, TimeSpan.FromMilliseconds(50), TestToken)).Dispose());
        }

        using (cache.Change(collidingKey, DefaultTimeout, TestToken))
        {
        }
    }

    private sealed class IntCacheWithWeight(int cacheCapacity, int collisionThreshold)
        : RandomAccessCache<int, long, long>(cacheCapacity, 0L, collisionThreshold)
    {
        protected override void AddWeight(ref long total, int key, long value)
            => Interlocked.Add(ref total, value);

        protected override bool IsEvictionRequired(ref readonly long total, int key, long value)
            => false;

        protected override void RemoveWeight(ref long total, int key, long value)
            => Interlocked.Add(ref total, -value);
    }

    private sealed class CacheWithStringWeight(int cacheCapacity, long maxWeight) : RandomAccessCache<string, string, long>(cacheCapacity, 0L, int.MaxValue)
    {
        private long totalWeight;
        private volatile bool nullObserved;

        public long TotalWeight => Volatile.Read(ref totalWeight);

        public bool NullObserved => nullObserved;

        private long GetWeight(string value)
        {
            if (value is null)
            {
                nullObserved = true;
                return 0L;
            }

            return value.Length;
        }

        protected override void AddWeight(ref long total, string key, string value)
        {
            var weight = GetWeight(value);
            Interlocked.Add(ref total, weight);
            Interlocked.Add(ref totalWeight, weight);
        }

        protected override bool IsEvictionRequired(ref readonly long total, string key, string value)
            => GetWeight(value) + total > maxWeight;

        protected override void RemoveWeight(ref long total, string key, string value)
        {
            var weight = GetWeight(value);
            Interlocked.Add(ref total, -weight);
            Interlocked.Add(ref totalWeight, -weight);
        }
    }

    private sealed class CacheWithWeight(int cacheCapacity, long maxWeight, int collisionThreshold, int concurrencyLevel = 4)
        : RandomAccessCache<string, long, long>(cacheCapacity, 0L, collisionThreshold, concurrencyLevel)
    {
        protected override void AddWeight(ref long total, string key, long value)
            => Interlocked.Add(ref total, value);

        protected override bool IsEvictionRequired(ref readonly long total, string key, long value)
            => value + total > maxWeight;

        protected override void RemoveWeight(ref long total, string key, long value)
            => Interlocked.Add(ref total, -value);
    }
}