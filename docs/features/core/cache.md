Concurrent Cache
====
[RandomAccessCache&lt;TKey,TValue&gt;](xref:DotNext.Runtime.Caching.RandomAccessCache`2) is a thread-safe and async-friendly cache based on [SIEVE](https://cachemon.github.io/SIEVE-website/) eviction algorithm. It's more efficient than LRU/LFU and doesn't require a lock for read-only operations. In case of cache hit, the performance of reading cache record is very fast and scalable.

> [!NOTE]
> The only support eviction policy is capacity-based eviction. Time-based eviction policy is not supported.

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