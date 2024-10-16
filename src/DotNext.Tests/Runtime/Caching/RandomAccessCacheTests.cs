using System.Runtime.CompilerServices;

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
            False(handle.TryGetValue(out _));

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
    public static async Task AddRemove()
    {
        await using var cache = new RandomAccessCache<long, string>(15);

        using (var session = await cache.ChangeAsync(10L))
        {
            False(session.TryGetValue(out _));
            session.SetValue("10");
        }

        Null(await cache.TryRemoveAsync(11L));

        using (var session = (await cache.TryRemoveAsync(10L)).Value)
        {
            Equal("10", session.Value);
        }
    }

    [Fact]
    public static async Task AddInvalidate()
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
}