Concurrent Cache
====
[ConcurrentCache&lt;TKey,TValue&gt;](xref:DotNext.Runtime.Caching.ConcurrentCache`2) is a thread-safe cache that provides API similar to [ConcurrentDictionary&lt;TKey,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2). It supports [LRU](https://en.wikipedia.org/wiki/Cache_replacement_policies#Least_recently_used_(LRU)) and [LFU](https://en.wikipedia.org/wiki/Cache_replacement_policies#Least-frequently_used_(LFU)) eviction policies. The cache is limited only by size in contrast to [System.Runtime.Caching.MemoryCache](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.caching.memorycache) or [Microsoft.Extensions.Caching.Memory.MemoryCache](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycache).

> [!NOTE]
> Time-based eviction policy is not supported.

The following example demonstrates how to create the cache:
```csharp
using DotNext.Runtime.Caching;

var cache = new ConcurrentCache<int, string>(100, CacheEvictionPolicy.LRU)
{
    Eviction = static (int key, string value) => Console.WriteLine($"Evicted entry: key = {key}, value = {value}"),
};

cache.TryAdd(42, "Hello, world!");
cache.TryGetValue(42, out string result);
```

`ConcurrentCache<TKey, TValue>.Count` property has O(1) time complexity. The cache supports asynchronous invocation that allows to remove cleanup activites from cache consumption thread. This feature is disabled by default. Set `ExecuteEvictionAsynchronously` property to **true** to enable asynchronous eviction:
```csharp
var cache = new ConcurrentCache<int, string>(100, CacheEvictionPolicy.LRU)
{
    Eviction = static (int key, string value) => Console.WriteLine($"Evicted entry: key = {key}, value = {value}"),
    ExecuteEvictionAsynchronously = true,
};
```