Structured Memory Access
====
[UnmanagedMemory&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory-1.yml) is a safe way to allocate structured data in unmanaged memory of any blittable value type. There are two possible usages of this type:
1. Allocation of an array in unmanaged memory
1. Allocation of blittable value type in unmanaged memory

Instance of `UnmanagedMemory<T>` is a managed object which maintains a pointer to the allocated unmanaged memory. Therefore, it can be reclaimed by GC. In that case, finalizer method releases unmanaged memory referenced by such object. Additionally, it is possible to call `Dispose()` method directly to release unmanaged memory.

# Unmanaged Array
The following example demonstrates how to allocate array in unmanaged memory.

```csharp
using DotNext.Runtime.InteropServices;

using(var memory = new UnmanagedMemory<double>(10)) //array of 10 elements
{
    Span<double> array = memory;
    //element set
    array[0] = 10;
    array[1] = 30;
    //obtains a pointer to the array element with index 1
    Pointer<double> ptr = array;
    ptr += 1;  
    //change array element
    ptr.Value = 30;
    //copy to managed heap
    double[] managedArray = array.ToArray();
}
```

Unmanaged array supports interoperation with managed arrays and streams.

**foreach** loop is also supported because `UnmanagedMemory<T>` is convertible into [Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1):

```csharp
using DotNext.Runtime.InteropServices;

using(var array = new UnmanagedMemory<double>(10))
{
    array[0] = 10;
    array[1] = 30;
    foreach(var item in array.Span)
        Console.WriteLine(item);
}
```

The memory can be resized on-the-fly. Resizing causes re-allocation of the memory with the copying of the elements from the original location. The new size of the array can be defined using `Reallocate` method.

```csharp
using DotNext.Runtime.InteropServices;

using(var memory = new UnmanagedMemory<double>(10)) //memory.Length == 10L
{
    Span<double> array = memory;
    array[0] = 10;
    array[1] = 30;
    memory.Reallocate(20);  //causes re-allocation of the array
    array = memory;
    var i = array[0] + array[1];    //i == 40
}
```

`Reallocate` method accepts the new length of the array, not size in bytes.

Memory span and typed pointer are not valid after re-allocation. You must ensure that consumers obtain a fresh version of there structures every time when working with unmanaged memory.

# struct Allocation
The same type can be used to allocate single value of blittable value type in unmanaged memory. To do that, you can specify length of _1_ when creating instance of `UnmanagedMemory<T>` or use `Box` static method to copy value from stack into unmanaged memory.

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

using(var memory = new UnmanagedMemory<Complex>(1))
{
    memory.Pointer.Image = 20;
    memory.Pointer.Real = 30;
}
```

Additionally, it is possible to box value type into unmanaged memory:
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using(var c = UnmanagedMemory<Complex>.Box(new Complex { Image = 20, Real = 30 }))
{
}
```

Direct memory manipulations available using typed pointer:
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using(var c = UnmanagedMemory<Complex>.Box(new Complex { Image = 20, Real = 30 }))
{
    Pointer<double> pImage = c.Pointer.As<double>();
    Pointer<double> pReal = pImage + 1;
    pImage.Value = 1;
    pReal.Value = 2;
}
```

Byte-level manipulations can be organized in two ways:
1. Through `Bytes` property which provides memory span for bytes
1. Through `Pointer` at its ability to reinterpret its type

```csharp
using DotNext.Runtime.InteropServices;
using System;

var id = Guid.NewGuid();

using(var memory = UnmanagedMemory<Guid>.Box(id))
{
    Span<byte> bytes = memory.Bytes;
    Pointer<byte> ptr = memory.Pointer.As<byte>();
}
```

# Unmanaged Memory Pool
.NET standard library provides a concept of [memory manager](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorymanager-1) which represents an owner of continuous block of memory. By default, it is implemented by [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1). The block of memory can be obtained from the manager in the form of [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) value type which is compatible with async methods. Unfortunately, the unmanaged memory controlled by `UnmanagedMemory<T>` class from .NEXT library is not compatible with `Memory<T>` value type.

This gap is closed by [UnmanagedMemoryPool&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.UnmanagedMemoryPool-1.html) class which allows to allocate a block of unmanaged memory in full compliance with [IMemoryOwner&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.imemoryowner-1) interface. Now it is possible to represent unmanaged memory as `Memory<T>` value type.

> [!TIP]
> `UnmanagedMemoryPool<T>` is preferred way to work with unmanaged memory and replacement for `UnmanagedMemory` and `UnmanagedMemory<T>` classes from .NEXT. However, re-allocation of unmanaged memory is not supported.

The block of unmanaged memory can be allocated using static method `Allocate` that has [IUnmanagedMemoryOwner&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.IUnmanagedMemoryOwner-1.html) return type. This interface is derived from [IMemoryOwner&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.imemoryowner-1) so the memory block can be easily represented as `Memory<T>` value type.

If you need to work with unmanaged memory as with memory pool then it is possible to instantiate `UnmanagedMemoryPool<T>` class and use it in the same manner as `MemoryPool<T>` from .NET standard library.

The following example demonstrates how to allocate a block of unmanaged memory and wrap it into `Memory<T>` value type:
```csharp
using DotNext.Buffers;
using System.Buffers;

using(IUnmanagedMemoryOwner<long> owner = UnmanagedMemoryPool<long>.Allocate(12))
{
  Memory<long> memory = owner.Memory;
  memory.Span[0] = 42L;
}
```
