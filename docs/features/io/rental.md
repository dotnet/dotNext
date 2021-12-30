Alloc vs Pooling
=====
.NET allows to rent a memory instead of allocation using **new** keyword. It is useful in many cases especially when you need a large block of memory or large array. There a many articles describing benefits of this approach.
* [Pooling large arrays with ArrayPool](https://adamsitnik.com/Array-Pool/)
* [Avoid GC Pressure](https://michaelscodingspot.com/avoid-gc-pressure/)

The memory can be rented using [ArrayPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) or [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1) but their direct usage has several inconveniences:
* Not possible to use **using** statement to return rented array back to the pool in case of `ArrayPool<T>`
* It's hard to mix the code when rental is optional. For instance, in case of small block of memory you can use **stackalloc** instead of renting memory
* The returned memory or array can have larger size so you need to control bounds by yourself

.NEXT offers convenient wrappers that simplify the rental process and handle situations when renting is optional:
* [MemoryRental&lt;T&gt;](xref:DotNext.Buffers.MemoryRental`1) if you need to work with [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1)
* [MemoryOwner&lt;T&gt;](xref:DotNext.Buffers.MemoryOwner`1) if you need universal mechanism to represent pooled memory obtained from [ArrayPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) or [MemoryPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1)

# MemoryRental
[MemoryRental&lt;T&gt;](xref:DotNext.Buffers.MemoryRental`1) helps to reduce boilerplate code in `stackalloc` vs memory pooling scenario. The rented memory is only accessible using [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type.

The following example demonstrates how to reverse a string and choose temporary buffers allocation method depending on the size of the string.
```csharp
using DotNext.Buffers;

public static unsafe string Reverse(this string str)
{
  if (str.Length == 0) return str;
  const int stackallocThreshold = 128;
  using MemoryRental<char> result = str.Length <= stackallocThreshold ? new MemoryRental<char>(stackalloc char[stackallocThreshold], str.Length) : new MemoryRental<char>(str.Length);
  str.AsSpan().CopyTo(result.Span);
  result.Span.Reverse();
  fixed (char* ptr = result.Span)
    return new string(ptr, 0, result.Length);
} 
```

`MemoryRental` can rent the memory using the following mechanisms:
* Memory pooling using arbitrary [MemoryPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1)
* Array pooling using [ArrayPool&lt;T&gt;.Shared](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.shared). Arbitrary array pool is not supported.

The type is typically used in unsafe context when you need a temporary buffer to perform in-memory transformations.

# MemoryOwner
.NET offers two different models for memory pooling: [MemoryPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1) class and [ArrayPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) class. Both are abstract classes so it's not possible to unify memory pooling API. For instance, [configuration model](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions) for I/O pipe from .NET expecting `MemoryPool<T>` instance. If you want to use custom `ArrayPool<T>` then you need to write wrapper for it.

.NEXT library contains [MemoryOwner&lt;T&gt;](xref:DotNext.Buffers.MemoryOwner`1) value type that represents rented memory regardless of the rental method:
```csharp
using DotNext.Buffers;
using System.Buffers;

using var rentedArray = new MemoryOwner<byte>(ArrayPool<byte>.Shared, 10);
using var rentedMemory = new MemoryOwner<byte>(MemoryPool<byte>.Shared, 10);
rentedArray.Memory.Slice(0, 5);
rentedMemory.Memory.Slice(0, 5);
```
The value type implements [IMemoryOwner&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.imemoryowner-1) interface so you can easly access pooled memory in a uniform way.

Additionally, .NEXT offers special abstraction layer for memory pooling which is compatible with existing mechanisms in .NET. [MemoryAllocator&lt;T&gt;](xref:DotNext.Buffers.MemoryAllocator`1) delegate represents universal way to rent the memory. The consumer of your library can supply concrete instance of this delegate to supply appropriate allocation mechanism. [MemoryAllocator](xref:DotNext.Buffers.MemoryAllocator) static class provides extension methods for interop between memory allocator and existing .NET memory pooling APIs.

# Growable Buffers
If size of the required buffer is not known and can grow dynamically then you need to use [Dynamic Buffers](./buffers.md) that are based on memory pooling mechanism as well.

Dynamic buffers can be combined with [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) easily using extension methods from [StreamSource](xref:DotNext.IO.StreamSource) class. With [PooledArrayBufferWriter&lt;T&gt;](xref:DotNext.Buffers.PooledArrayBufferWriter`1) class, it's possible to read/write bytes using stream and utilize memory pooling:
```csharp
using DotNext.Buffers;
using DotNext.IO;

using var writer = new PooledArrayBufferWriter<byte> { BufferPool = ArrayPool<byte>.Shared };

// write bytes using stream
using Stream writeStream = StreamSource.AsStream(writer);
writeStream.Write(new byte[1024]);

// read bytes using stream
using Stream readStream = StreamSource.AsStream(writer.WrittenMemory);
```

# Tuning
Many operations in .NEXT use stack-allocated memory in combination with `MemoryRental<T>` type for better performance. It's possible to configure the size of stack-allocated memory used as initial buffer using `DOTNEXT_STACK_ALLOC_THRESHOLD` environment variable. By default, it is equal to _512_ bytes. It means that if the size of the data inside of I/O operations is less that this value then stack memory will be used. Otherwise, the buffer will be rented from the pool.