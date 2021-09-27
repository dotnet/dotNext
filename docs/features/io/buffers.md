Dynamic Buffers
====
[ArrayBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraybufferwriter-1) represents default implementation of dynamically-sized, heap-based and array-backed buffer. Unfortunately, it's not flexible enough in the following aspects:
* Not possible to use array or memory pooling mechanism. As a result, umnanaged memory cannot be used for such writer.
* Not compatible with [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1)
* No easy way to obtain stream over written memory
* Allocation on the heap

With .NEXT, you have this flexibility.

# PooledBufferWriter
[PooledBufferWriter&lt;T&gt;](xref:DotNext.Buffers.PooledBufferWriter`1) is similar to `ArrayBufferWriter` but accepts [memory allocator](xref:DotNext.Buffers.MemoryAllocator`1) that is used for allocation of internal buffers. Thus, you can use any pooling mechanism from .NET: memory or array pool. If writer detects that capacity exceeded then it rents a new internal buffer and copies written content from previous one. 
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
[PooledArrayBufferWriter&lt;T&gt;](xref:DotNext.Buffers.PooledArrayBufferWriter`1) class exposes the similar functionality to `PooledBufferWriter` class but specialized for working with [ArrayPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1). As a result, you can make writes or obtain written memory using [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1).
```csharp
using DotNext.Buffers;
using DotNext.IO;
using System;

using var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared);
ArraySegment<byte> array = writer.GetArray(1024);
array[0] = 42;
array[1] = 43;
span.Advance(2);
ArraySegment<byte> result = writer.WrittenArray;
```

Additionally, it implements [IList&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ilist-1) interface so you can use it as fully-functional list which rents the memory instead of allocation on the heap.

# Sparse Buffer
[SparseBufferWriter&lt;T&gt;](xref:DotNext.Buffers.SparseBufferWriter`1) represents a writer for the buffer represented by a set of non-contiguous memory blocks. Its main advantage over previously described buffer types is a monotonic growth without reallocations. If the buffer has not enough space to place a new portion of data then it just allocates another contiguous buffer from the pool and attaches it to the end of the chain of buffers. Thus, the buffer growth has deterministic performance.

Additionally, sparse buffer allows to import memory blocks without copying them to the rented buffer. For instance, a memory block represented by [ReadOnlyMemory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1) can be intermixed with the memory blocks rented by the sparse buffer internally.
```csharp
using DotNext.Buffers;
using System;

var array = new byte[] { 10, 20, 30 };
using var writer = new SparseBufferWriter<byte>();
writer.Write(array.AsMemory(), false);  // false means that the memory block must be inserted into sparse buffer as-is without copying its content to the internal buffer
```

Sparse buffer writer also implements [IBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface as well as other buffer writers mentioned above. However, this interface is implemented explicitly and its methods should be used with care. Major drawback of this buffer type is that it can produce memory holes, i.e. unused memory segments in the middle of the buffer chunks. The holes can be caused by `IBufferWriter<T>.GetMemory(int)` or `IBufferWriter<T>.GetSpan(int)` implementations. Therefore these methods are implemented explicitly. All other public methods of `SparseBufferWriter<T>` class cannot cause memory holes.

Suppose that sparse buffer has rented memory block of size _1024_ bytes, and _1000_ bytes of them already occupied. If you want to write a block of size _100_ bytes represented by [ReadOnlySpan&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1) then use `SparseBufferWriter<T>.Write(ReadOnlySpan<T>)` method. It writes the first _24_ bytes to the existing memory block and then rents a new segment to store the rest of the input buffer, _76_ bytes. Therefore, `Write` method cannot cause fragmentation of memory blocks. However, if we want to obtain a memory block for writing via `GetMemory(int)` method then sparse buffer cannot utilize _24_ bytes of free memory from the existing chunk because the returned buffer must be at least _100_ bytes of contiguous memory. In this case, sparse buffer rents a new chunk with the size of at least _100_ bytes and marks _24_ bytes from the previous chunk as unused.

The implementation of `GetMemory(int)` and `GetSpan(int)` methods are optimized to reduce the number of such memory holes. However, due to nature of sparse buffer data structure, it is not possible in 100% cases. Nevertheless, such overhead can be acceptable because sparse buffer never reallocates the existing memory and may work faster than [PooledBufferWriter&lt;T&gt;](xref:DotNext.Buffers.PooledBufferWriter`1) which requires reallocation when rented memory block is not enough to place a new data.

Additionally, you can use [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream)-based API to read from or write to the sparse buffer. [StreamSource](xref:DotNext.IO.StreamSource) provides `AsStream` extension method that can be used to create readable or writable stream over the buffer:
```csharp
using DotNext.Buffers;
using DotNext.IO;

using var buffer = new SparseBufferWriter<byte>();
using Stream writable = buffer.AsStream(false); // create writable stream
using Stream readable = buffer.AsStream(true);  // create readable stream
```

Sparse buffer supports various strategies for allocation of the memory chunks:
1. Default behavior when size of each memory chunk is the same
1. Linear growth, when the size of each new memory chunk is a multiple of the chunk index
1. Exponential growth, when each new memory chunk doubles in size

The first strategy is effective when potential max size of the resulting buffer is hundreds of elements and volatility is small. The last two are effective when max size of the result buffer can be potentially large (kilobytes) and volatility is unpredictable.

The following example demonstrates usage of exponential growth strategy with predefined size of initial memory chunk:
```csharp
using DotNext.Buffers;

using var buffer = new SparseBufferWriter<byte>(256, SparseBufferGrowth.Exponential);
```

# BufferWriterSlim
[BufferWriterSlim&lt;T&gt;](xref:DotNext.Buffers.BufferWriterSlim`1) is a lightweight version of [PooledBufferWriter&lt;T&gt;](xref:DotNext.Buffers.PooledBufferWriter`1) class with its own unique features. The instance of writer always allocated on the stack because the type is declared as ref-like value type. Additionally, the writer allows to use stack-allocated memory for placing new elements.

If initial buffer overflows then `BufferWriterSlim<T>` obtains rented buffer from the pool and copies the initial buffer into it.
```csharp
using DotNext.Buffers;

using var builder = new BufferWriterSlim<byte>(stackalloc byte[128]); // capacity can be changed at runtime
builder.Write(new byte[] { 1, 2, 3 });
ReadOnlySpan<byte> result = builder.WrittenSpan;
```

This type has the following limitations:
* Incompatible with async methods
* Focused on [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type, there is no interop with types from [System.Collections.Generic](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic) namespace.

# Char Buffer
[StringBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder) is a great tool from .NET standard library to construct strings dynamically. However, it uses heap-based allocation of chunks and increases GC workload. The solution is to use pooled memory for growing buffer and release it when no longer needed. This approach is implemented by `PooledBufferWriter<T>`, `PooledArrayBufferWriter<T>`, `SparseBufferWriter<T>` and `BufferWriterSlim<T>` types as described above. But we need suitable methods for adding characters to the builder similar to the methods of `StringBuilder`. They are provided as extension methods declared in [BufferWriter](xref:DotNext.Buffers.BufferWriter) class for all objects implementing [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface:
```csharp
using DotNext.Buffers;

using var writer = new PooledArrayBufferWriter<char>(ArrayPool<char>.Shared);
writer.Write("Hello,");
writer.Write(' ');
writer.Write("world!");
writer.WriteLine();
writer.WriteFormattable(2);
writer.Write('+');
writer.WriteFormattable(2);
writer.Write('=');
writer.WriteFormattable(4);

string result = writer.ToString();
```

[TextWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter) is a common way to produce text dynamically and recognizable by many third-party libraries. There is a bridge that allow to use TextWriter API over pooled buffer writer with help of extension methods declared in [TextStreamExtensions](xref:DotNext.IO.TextStreamExtensions) class:
```csharp
using DotNext.Buffers;
using System.IO;
using static DotNext.IO.TextWriterExtensions;

using var buffer = new PooledArrayBufferWriter<char>(ArrayPool<char>.Shared);
using TextWriter writer = buffer.AsTextWriter();
writer.Write("Hello,");
writer.Write(' ');
writer.Write("world!");
writer.WriteLine();
writer.Write(2);
writer.Write('+');
writer.Write(2);
writer.Write('=');
writer.Write(4);

string result = buffer.ToString();
```

C# 10 introduces a new implementation of [string interpolation](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) using Interpolated String Handlers. This approach is fully supported by `WriteString` extension methods for [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) and [BufferWriterSlim&lt;char&gt;](xref:DotNext.Buffers.BufferWriterSlim`1) data types. Now string building can have zero memory allocation overhead:
```csharp
using DotNext.Buffers;

int x = 10, y = 20;
using var buffer = new BufferWriterSlim<char>(stackalloc char[128]);
buffer.WriteString($"{x} + {y} = {x + y}");

string result = buffer.ToString();
```

Alignment and custom formats are fully supported. For more information about these interpolated string handlers, see [BufferWriterSlimInterpolatedStringHandler](xref:DotNext.Buffers.BufferWriterSlimInterpolatedStringHandler) and [BufferWriterInterpolatedStringHandler](xref:DotNext.Buffers.BufferWriterInterpolatedStringHandler) data types.

# What to choose?
The following table describes the main differences between various growable buffer types:

| Buffer Writer | When to use | Compatible with async methods | Space complexity (write operation) |
| ---- | ---- | ---- | ---- |
| `PooledArrayBufferWriter<T>` | General applicability when initial capacity is known | Yes | o(1), O(n) |
| `PooledBufferWriter<T>` | If custom [memory allocator](xref:DotNext.Buffers.MemoryAllocator`1) is required. For instance, if you want to use [unmanaged memory pool](xref:DotNext.Buffers.UnmanagedMemoryPool`1) | Yes | o(1), O(n) |
| `BufferWriterSlim<T>` | If you have knowledge about optimal size of initial buffer which can be allocated on the stack. In this case the writer allows to avoid renting the buffer and doesn't allocate itself on the managed heap | No | o(1), O(n) |
| `SparseBufferWriter<T>` | If optimal size of initial buffer is not known and the length of the written data varies widely | Yes | o(1), O(1) |

`SparseBufferWriter<T>` is more reusable than [RecyclableMemoryStream](https://www.nuget.org/packages/Microsoft.IO.RecyclableMemoryStream/) because it can be instantiated with any generic argument, not only with **byte**. Moreover, the [benchmark](../../benchmarks.md) demonstrates the better results for large memory blocks.