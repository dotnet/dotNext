Dynamic Buffers
====
[ArrayBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraybufferwriter-1) represents default implementation of dynamically-sized, heap-based and array-backed buffer. Unfortunately, it's not flexible enough in the following aspects:
* Not possible to use array or memory pooling mechanism. As a result, umnanaged memory cannot be used for such writer.
* Not compatible with [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1)
* No easy way to obtain stream over written memory
* Allocation on the heap

With .NEXT, you have this flexibility.

# PooledBufferWriter
[PooledBufferWriter&lt;T&gt;](../../api/DotNext.Buffers.PooledBufferWriter-1.yml) is similar to `ArrayBufferWriter` but accepts [memory allocator](../../api/DotNext.Buffers.MemoryAllocator-1.yml) that is used for allocation of internal buffers. Thus, you can use any pooling mechanism from .NET: memory or array pool. If writer detects that capacity exceeded then it rents a new internal buffer and copies written content from previous one. 
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
[PooledArrayBufferWriter&lt;T&gt;](../../api/DotNext.Buffers.PooledArrayBufferWriter-1.yml) class exposes the similar functionality to `PooledBufferWriter` class but specialized for working with [ArrayPool&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1). As a result, you can make writes or obtain written memory using [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1).
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

# String Buffer
[StringBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder) is a great tool from .NET standard library to construct strings dynamically. However, it uses heap-based allocation of chunks and increases GC workload. The solution is to use pooled memory for growing buffer and release it when no longer needed. This approach is implemented by `PooledBufferWriter<T>` and `PooledArrayBufferWriter<T>` classes as described above. But we need suitable methods for adding portions of data to the builder similar to the methods of `StringBuilder`. They are provided as extension methods declared in [BufferWriter](../../api/DotNext.Buffers.BufferWriter.yml) class for all objects implementing [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface:
```csharp
using DotNext.Buffers;

using var writer = new PooledArrayBufferWriter<char>(ArrayPool<char>.Shared);
writer.Write("Hello,");
writer.Write(' ');
writer.Write("world!");
writer.WriteLine();
writer.Write(2);
writer.Write('+');
writer.Write(2);
writer.Write('=');
writer.Write(4);

string result = writer.BuildString();
```

[TextWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter) is a common way to produce text dynamically and recognizable by many third-party libraries. There is a bridge that allow to use TextWriter API over pooled buffer writer with help of extension methods declared in [TextWriterSource](../../api/DotNext.IO.TextWriterSource.yml) class:
```csharp
using DotNext.Buffers;
using System.IO;
using static DotNext.IO.TextWriterSource;

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

string result = buffer.BuildString();
```

# BufferWriterSlim
[BufferWriterSlim&lt;T&gt;](../../api/DotNext.Buffers.BufferWriterSlim-1.yml) is a lightweight version of [PooledBufferWriter&lt;T&gt;](../../api/DotNext.Buffers.PooledBufferWriter-1.yml) class with its own unique features. The instance of writer always allocated on the stack because the type is declared as ref-like value type. Additionally, the writer allows to use stack-allocated memory for placing new elements.

The type supports the following modes:
* **Non-contiguous memory re-allocation** mode utilizes pooled memory to place new elements if capacity of pre-allocated buffer exceeded. However, added elements are placed to non-contiguous memory (i.e. divided pre-allocated buffer and pooled memory). In other words, the head of the list of added elements is always in pre-allocated buffer.
* **Contiguous memory re-allocation** ensures that all elements are placed in contiguous memory, thus can be obtained as [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1). The writer trying to place new elements to pre-allocated buffer. If it's capacity exceeded then the whole content of such buffer moving to pooled memory. Therefore, head and tail are not divided on two buffers.


The first mode doesn't allow to obtain written memory as [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1).
```csharp
using DotNext.Buffers;

using var builder = new SpanBuilder<byte>(stackalloc byte[128], false); // capacity can be changed at runtime
builder.Write(new byte[] { 1, 2, 3 });
ReadOnlySpan<byte> result = builder.WrittenSpan;    // throw NotSupportedException if the content is in non-contiguous memory
```

To enable usage of contiguous memory, just pass **true** to constructor in the example above instead of **false** constant.

This type has the following limitations:
* Incompatible with async methods
* Focused on [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type, there is no interop with types from [System.Collections.Generic](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic) namespace