Concurrent Cache
====
[RandomAccessCache&lt;TKey,TValue&gt;](xref:DotNext.Runtime.Caching.RandomAccessCache`2) is a thread-safe and async-friendly cache based on [SIEVE](https://cachemon.github.io/SIEVE-website/) eviction algorithm. It's more efficient than LRU/LFU and does not require a lock for read operations. In case of cache hit, the performance of reading cache record is very fast and scalable.

> [!NOTE]
> Time-based eviction policy is not supported.

# Capacity-based cache
[RandomAccessCache&lt;TKey,TValue&gt;](xref:DotNext.Runtime.Caching.RandomAccessCache`2) is capacity-based cache. The cache can keep the specified maximum amount of items. When the capacity exceeds, the eldest item is evicted. The eviction process doesn't interfere with writers or readers, it runs in the background.

All methods of the cache are asynchronous except `TryRead` that is designed to be extremely fast in case of cache hit. `ChangeAsync` is designed to be asynchronous because in case of cache miss the application needs to load the resource asynchronously. In the same time, multiple callers of cache trying to cache the same resource. This is a waste of resources because at least one caller is already in the process of resource loading. For other callers, it's better to wait. The following example demonstrates this pattern:
```csharp
using DotNext.Runtime.Caching;

var cache = new RandomAccessCache<string, byte[]>(100);

async ValueTask<byte[]> ReadFile(string fileName)
{
    if (cache.TryRead(fileName, out var readSession))
    {
        // cache hit, the method can be completed synchronously
        using (readSession)
        {
            return readSession.Value;
        }
    }
    else
    {
        // Cache miss, load the file from file system.
        // Other callers reached this branch are suspended
        using var writeSession = await cache.ChangeAsync(fileName);
        
        // Resumed caller can see the value loaded by another concurrent flow, no need to load resource again
        if (!writeSession.TryGetValue(out byte[] value))
        {
            // load resource asynchronously
            value = await LoadFileFromDiskAsync(fileName);
            writeSession.SetValue(value);
        }
        
        return value;
    }
}
```

Until the session disposed, the cache record cannot be evicted. The concept of session is needed because the eviction process runs concurrently with the callers accessing the cache. In the same time, the cache evicts the record as soon as possible. Under the hood, the record chosen for eviction passes two phases: mark and sweep. The record marked for eviction can be in use by concurrent thread. In that case, eviction algorithm cannot sweep it. Instead, sweep phase is delayed while there is at least one reader of this value. The reader is an active session. When the last session closes, sweep phase executes immediately and the control passes to the custom eviction callback.

# Weight-based cache
[RandomAccessCache&lt;TKey,TValue, TWeight&gt;](xref:DotNext.Runtime.Caching.RandomAccessCache`3) represents weight-based cache. Capacity-based cache is a special case of the weight-based cache when the weight of every item is 1. With weight-based cache, it's possible to define a custom algorithm to detect the weight of the item based on key/value pair. Under the hood, it's the same SIEVE algorithm. Weight-based cache is an abstract class because the user needs to provide a logic for weight calculation.

Weight-based cache can grow over the time in contrast to capacity-based cache. To resize the internal data structures, the cache needs to acquire a global lock across all the items in the cache. It's heavyweight operation. Any writer suspends during the resize. Readers are not impacted. This behavior can be tuned with `collisionThreshold` constructor parameter:
* Higher value of `collisionThreshold` reduces the chance of the resize by the cost of potential contention between writers
* Lower value of `collisionThreshold` reduces the chance of contention between writes by the cost of increased chance of the resize
* [int.MaxValue](https://learn.microsoft.com/en-us/dotnet/api/system.int32.maxvalue) can be specified to disable the resize at all. In that case, the chance of the contention between grows linearly with the number of items in the cache.