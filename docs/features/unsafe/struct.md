Structured Memory Access
====
.NET standard library provides a concept of [memory manager](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorymanager-1) which represents an owner of continuous block of memory. By default, it is implemented by [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1). The block of memory can be obtained from the manager in the form of [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) value type which is compatible with async methods. Unfortunately, the unmanaged memory controlled by `UnmanagedMemory<T>` class from .NEXT library is not compatible with `Memory<T>` value type.

This gap is closed by [UnmanagedMemoryPool&lt;T&gt;](xref:DotNext.Buffers.UnmanagedMemoryPool`1) class which allows to allocate a block of unmanaged memory in full compliance with [IMemoryOwner&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.imemoryowner-1) interface. Now it is possible to represent unmanaged memory as `Memory<T>` value type.

The block of unmanaged memory can be allocated using static method `Allocate` that has [IUnmanagedMemory&lt;T&gt;](xref:DotNext.Runtime.InteropServices.IUnmanagedMemory`1) return type. This interface is derived from [IMemoryOwner&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.imemoryowner-1) so the memory block can be easily represented as `Memory<T>` value type.

If you need to work with unmanaged memory as with memory pool then it is possible to instantiate `UnmanagedMemoryPool<T>` class and use it in the same manner as `MemoryPool<T>` from .NET standard library.

The following example demonstrates how to allocate a block of unmanaged memory and wrap it into `Memory<T>` value type:
```csharp
using DotNext.Buffers;
using System.Buffers;

using IUnmanagedMemory<long> owner = IUnmanagedMemory<long>.Allocate(12);
Memory<long> memory = owner.Memory;
memory.Span[0] = 42L;
```

`MemoryAllocator<T>.Unmanaged` property exposes the allocator of the unmanaged memory.

The memory can be resized on-the-fly. Resizing causes re-allocation of the memory with the copying of the elements from the original location. The new size of the array can be defined using `Reallocate` method.

```csharp
using DotNext.Buffers;

using var block = IUnmanagedMemory<double>.Allocate(10); //block.Length == 10L
Span<double> array = block.Memory.Span;
array[0] = 10;
array[1] = 30;
block.Reallocate(20);  //causes re-allocation of the array
array = memory.Memory.Span;
var i = array[0] + array[1];    //i == 40
```

`Reallocate` method accepts the new length of the array, not size in bytes.

Memory span and typed pointer are not valid after re-allocation. You must ensure that consumers obtain a fresh version of there structures every time when working with unmanaged memory.

# struct Allocation
[UnmanagedMemory&lt;T&gt;](xref:DotNext.Runtime.InteropServices.UnmanagedMemory`1) can be used to allocate blittable value type in unmanaged memory.

The simpliest way to understand this concept is to provide the following example in C:
```c
#include <stdlib.h>

typedef struct {
    double image;
    double real;
} complex;

complex *c = malloc(sizeof(complex));
c->image = 20;
c->real = 30;
free(c);
```

The equivalent code in C# using `UnmanagedMemory<T>` is
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using var memory = new UnmanagedMemory<Complex>();
ref Complex ptr = ref memory.Pointer.Value;
ptr.Image = 20;
ptr.Real = 30;
```

Direct memory manipulations available using typed pointer:
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using var c = new UnmanagedMemory<Complex>(new { Image = 20, Real = 30 });
Pointer<double> pImage = c.Pointer.As<double>();
Pointer<double> pReal = pImage + 1;
pImage.Value = 1;
pReal.Value = 2;
```

Byte-level manipulations can be organized in two ways:
1. Through `Bytes` property which provides memory span for bytes
1. Through `Pointer` and its ability to reinterpret pointer type

```csharp
using DotNext.Runtime.InteropServices;
using System;

var id = Guid.NewGuid();

using var memory = new UnmanagedMemory<Guid>(id);
Span<byte> bytes = memory.Bytes;
Pointer<byte> ptr = memory.Pointer;
```