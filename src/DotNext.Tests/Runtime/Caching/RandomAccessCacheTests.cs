using System.Collections.Concurrent;

namespace DotNext.Runtime.Caching;

public sealed class RandomAccessCacheTests : Test
{
    [Fact]
    public static async Task CacheOverflow()
    {
        var evictedItem = new TaskCompletionSource<string>();
        await using var cache = new RandomAccessCache<long, string>(15)
        {
            OnEviction = (_, value) => evictedItem.TrySetResult(value),
        };

        for (long i = 0; i < 17; i++)
        {
            using var handle = await cache.ModifyAsync(i);
            False(handle.TryGetValue(out _));

            handle.SetValue(i.ToString());
        }

        Equal("0", await evictedItem.Task.WaitAsync(DefaultTimeout));
    }
}