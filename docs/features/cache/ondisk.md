On-disk cache
====
Typical use case of [RandomAccessCache&lt;TKey,TValue&gt;](xref:DotNext.Runtime.Caching.RandomAccessCache`2) class is to keep the values loaded to the memory. This is called **L0 cache**, because all the items are quickly accessible through the memory. But the memory is a limited resource. In some situations, the disk space can be used as **L1 cache**, because typically the disk has more space than RAM. However, it's slower. The cache can be organized as a set of layers: if there is no cache item in L0, try L1. Instead of keeping items in the memory, they can be stored on the disk in serialized form. Both capacity-based and weight-based caches are suitable for that.

Instead of keeping the entire item, the cache can keep the reference to the data on the disk. [DiskSpacePool](xref:DotNext.Runtime.Caching.DiskSpacePool) is designed especially for that purpose. It acts as a pool of segments stored on the disk. Every segment has maximum size, and it's represented by an object which size doesn't depend on the size of the cached item:
```csharp
using DiskSpacePool pool = new(4096); // 4k is max entry size on the disk
DiskSpacePool.Segment segment = pool.Rent();
await segment.WriteAsync(data);
```

Once the segment rented, it can be placed as a value of the cache. `DiskSpacePool.Segment` is just a reference to the disk-allocated space. You need to use read/write methods of the class to obtain the cache item.

The pool tries to minimize the impact on the page cache of the OS. Thus, every read of the segment bypasses the cache and asks the kernel to fill the read buffer directly from the disk. This is acceptable trade-off, because L0 cache has in-memory cached items for the fast access. Moreover, L1 cache capacity can be magnitude larger than the capacity of L0 cache. In that case, it's not reasonable to keep the data in the page cache.