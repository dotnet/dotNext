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
            using var handle = await cache.ChangeAsync(i);
            True(Unsafe.IsNullRef(in handle.ValueRefOrNullRef));

            handle.SetValue(i.ToString());
        }

        Equal("0", await evictedItem.Task.WaitAsync(DefaultTimeout));
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

        await using (var session = await cache.ChangeAsync(10L))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        Null(await cache.TryRemoveAsync(11L));
        True(cache.Contains(10L));

        using (var session = (await cache.TryRemoveAsync(10L)).Value)
        {
            Equal("10", session.Value);
        }

        False(cache.Contains(10L));
    }
    
    [Fact]
    public static void AddRemove()
    {
        using var cache = new RandomAccessCache<long, string>(15);

        using (var writeSession = cache.Change(10L, DefaultTimeout))
        {
            False(writeSession.TryGetValue(out _));
            writeSession.SetValue("10");
        }

        False(cache.TryRemove(11L, out _, DefaultTimeout));
        
        True(cache.Contains(10L));
        True(cache.TryRemove(10L, out var session, DefaultTimeout));
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

        using (var session = await cache.ChangeAsync(10L))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        False(await cache.InvalidateAsync(11L));
        True(await cache.InvalidateAsync(10L));
    }
    
    [Fact]
    public static void AddInvalidate()
    {
        using var cache = new RandomAccessCache<long, string>(15);

        using (var session = cache.Change(10L, DefaultTimeout))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        False(cache.Invalidate(11L, DefaultTimeout));
        True(cache.Invalidate(10L, DefaultTimeout));
    }

    [Fact]
    public static async Task AddTwice()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        using (var session = await cache.ChangeAsync(10L))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");

            Throws<InvalidOperationException>(() => session.SetValue("20"));
        }
    }

    [Fact]
    public static async Task DisposedCacheAccess()
    {
        var cache = new RandomAccessCache<long, string>(18);
        await cache.DisposeAsync();

        await ThrowsAsync<ObjectDisposedException>(cache.ChangeAsync(0L).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.TryRemoveAsync(0L).AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync().AsTask);
        await ThrowsAsync<ObjectDisposedException>(cache.InvalidateAsync(10L).AsTask);
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
            using var handle = await cache.ChangeAsync(i);
            False(handle.TryGetValue(out _));

            handle.SetValue(i.ToString());
        }

        await cache.InvalidateAsync();
    }

    [Fact]
    public static async Task ReplaceWhileReadingAsync()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        using (var handle = await cache.ChangeAsync(42L))
        {
            handle.SetValue("42");
        }

        True(cache.TryRead(42L, out var readSession));

        using (readSession)
        {
            using (var handle = await cache.ReplaceAsync(42L))
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

        using (var handle = cache.Change(42L, DefaultTimeout))
        {
            handle.SetValue("42");
        }

        True(cache.TryRead(42L, out var readSession));

        using (readSession)
        {
            using (var handle = cache.Replace(42L, DefaultTimeout))
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
    public static async Task EvictLargeItemImmediately()
    {
        const long value = 101L;
        var source = new TaskCompletionSource<long>();
        await using var cache = new CacheWithWeight(42, value - 1L, 3)
        {
            Eviction = (_, v) => source.TrySetResult(v),
        };
        
        await using (var session = await cache.ChangeAsync("101"))
        {
            False(session.TryGetValue(out _));
            session.SetValue(101L); // 101 > 100, must be evicted immediately
        }

        Equal(value, await source.Task.WaitAsync(DefaultTimeout));
    }

    [Fact]
    public static async Task EvictRedundantItems()
    {
        var channel = Channel.CreateBounded<long>(2);
        await using var cache = new CacheWithWeight(42, 100L, 3)
        {
            Eviction = (_, v) => True(channel.Writer.TryWrite(v)),
        };
        
        using (var session = await cache.ChangeAsync("60"))
        {
            False(session.TryGetValue(out _));
            session.SetValue(60L);
        }
        
        using (var session = await cache.ChangeAsync("40"))
        {
            False(session.TryGetValue(out _));
            session.SetValue(40L);
        }
        
        using (var session = await cache.ChangeAsync("100"))
        {
            False(session.TryGetValue(out _));
            session.SetValue(100L);
        }

        var x = await channel.Reader.ReadAsync();
        var y = await channel.Reader.ReadAsync();
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

    private sealed class CacheWithWeight(int cacheCapacity, long maxWeight, int collisionThreshold) : RandomAccessCache<string, long, long>(cacheCapacity, 0L, collisionThreshold)
    {
        protected override void AddWeight(ref long total, string key, long value)
            => Interlocked.Add(ref total, value);

        protected override bool IsEvictionRequired(ref readonly long total, string key, long value)
            => value + total > maxWeight;

        protected override void RemoveWeight(ref long total, string key, long value)
            => Interlocked.Add(ref total, -value);
    }
}