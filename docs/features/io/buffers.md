Dynamic Buffers
====
[ArrayBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraybufferwriter-1) represents default implementation of dynamically-sized, heap-based and array-backed buffer. Unfortunately, it's not flexible enough in the following aspects:
* Not possible to use array or memory pooling mechanism. As a result, umnanaged memory cannot be used for such writer.
* Not compatible with [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1)
* No easy way to obtain stream over written memory
* Allocation on the heap

With .NEXT, you have this flexibility.

# PooledBufferWriter
[PooledBufferWriter&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.PooledBufferWriter-1.html) is similar to `ArrayBufferWriter` but accepts [memory allocator](https://sakno.github.io/dotNext/api/DotNext.Buffers.MemoryAllocator-1.html) that is used for allocation of internal buffers. Thus, you can use any pooling mechanism from .NET: memory or array pool. If writer detects that capacity exceeded then it rents a new internal buffer and copies written content from previous one. 
```csharp
using DotNext.Buffers;

using var writer = new PooledBufferWriter<byte>(ArrayPool<byte>.Shared.ToAllocator());
Span<byte> span = writer.GetSpan(1024);
new byte[512].AsSpan().CopyTo(span);
span.Advance(512);
var result = writer.WrittenMemory;  //length is 512
```
In contrast to `ArrayBufferWriter`, you must not use written memory if `Dispose` is called. When `Dispose` method is called, the internal buffer returns to the pool.

# PooledArrayBufferWriter
[PooledArrayBufferWriter&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.PooledArrayBufferWriter-1.html) exposes the similar functionality to `PooledBufferWriter` class but specialized for working with [ArrayPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1). As a result, you can make writes or obtain written memory using [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1). Additionally, if the buffer is for bytes (actual generic argument is `byte`) then written memory can be exposed as read-only [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream) without copying written bytes.
```csharp
using DotNext.Buffers;
using DotNext.IO;
using System;

using var writer = new PooledBufferWriter<byte>(ArrayPool<byte>.Shared);
ArraySegment<byte> array = writer.GetArray(1024);
array[0] = 42;
array[1] = 43;
span.Advance(2);
ArraySegment<byte> result = writer.WrittenArray;  //length is 512
using Stream stream = StreamSource.GetWrittenBytesAsStream(writer);
```

One more powerful feature is that the class implements [IList&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ilist-1) interface so you can use it as fully-functional list which rents the memory instead of allocation on the heap.