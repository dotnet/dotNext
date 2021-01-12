Unsafe Data Structures
====
Historically, .NET Runtime was oriented to work with managed memory where Garbage Collector provides automatic memory management. The object placed into managed heap is reclaimed automatically when no longer needed. But numerous instantiation of thousand objects leads to GC pressure and may affect application performance. Manual memory management in some situations may fix this issue. 

.NET provides a lot of interop services to call external native C libraries or COM objects. Moreover, the standard library provides a way to [allocate unmanaged memory](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.allochglobal) outside of managed heap. But there is no routines for unmanaged memory manipulation. [Unsafe](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe), [Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) and [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1) are not equal to C memory functions and oriented to managed memory manipulation. _Span_ value type has special syntactic support in C# when stack allocated memory can be wrapped into this type:
```csharp
Span<int> s = stackalloc int[4];
```

.NEXT library provides rich data structures allocated in unmanaged heap (off-heap memory) and set of routines comparable to C memory functions. Moreover, these features are platform and hardware independent. 

> [!NOTE]
> The library is written in managed code without PInvoke calls.

The first feature is CLS compliant pointer data type with low-level memory manipulation methods. The second feature is a set of value types representing structured access to the off-heap memory:

1. [UnmanagedMemory&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory-1.yml) provides structured access to the unmanaged memory.
1. [UnmanagedMemory](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory.yml) provides unstructured access to the unmanaged memory.

All unmanaged data types are not thread-safe. However, there are various extension methods in [AtomicPointer](../../api/DotNext.Threading.AtomicPointer.yml) class supporting thread-safe manipulations with unmanaged memory. Additionally, these types are implicitly convertible into [Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type.

Both types are inherited from [UnmanagedMemoryHandle](../../api/DotNext.Runtime.InteropServices.UnmanagedMemoryHandle.yml). Direct manipulations with the memory are possible using the following approaches:
* `Pointer` property provides flexible manipulations using typed pointer to the memory. It is unsafe way because the pointer doesn't provide bound checks
* `Bytes` property provides memory access using [Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type. It is less flexible in comparison with pointer but safe because it implements bound checks

# Pointer
C# programming language supports typed and untyped pointer data types such as _int*_ or _void*_. These types are not CLS-compliant and may not be available in other .NET programming languages. For instance, F# or VB.NET do not support pointer type and interoperation with native libraries or COM objects are limited. 

.NEXT offers [Pointer&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.Pointer-1.yml) value type which is CLS-compliant typed pointer. This type supports pointer arithmetic as well as rich methods for memory manupulations such as copying, swapping, filling with zeroes, comparison and equality check. These methods are equivalent to C memory functions: `memset`, `memcmp` etc. Additionally, there are routines to copying bytes to/from the unmanaged memory from/to the arbitrary [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) asynchronously. Check out API documentation for detailed information about available methods.

The pointer data type is convertible to/from [native int](https://docs.microsoft.com/en-us/dotnet/api/system.intptr) so it can be used to simplify interoperation with native code. The size of the pointer depends on underlying platform.

The following example demonstrates usage of the typed pointer to work with stack-allocated memory:
```csharp
using DotNext.Runtime.InteropServices;
using System.IO;

private static void UnsafeMethod()
{
    Pointer<int> ptr = stackalloc int[4] { 1, 3, 6, 9 };
    ptr.Value = 10; //change value 1 to 10
    ptr += 2;   //pointer arithmetic. Now pointer points to the third element in stack-allocated array
    ptr.Value = 50; //change value 6 to 50
    //copy to stream
    using(Stream memory = new MemoryStream())
    {
        ptr.WriteTo(memory, 4);
    }
    //copy to array
    var array = new int[4];
    ptr.WriteTo(array); //now array = new int[] { 10, 3, 50, 9 }
}
```

Pointer can be re-interpreted:
```csharp
using DotNext.Runtime.InteropServices;

private static void UnsafeMethod()
{
    Pointer<int> ptr = stackalloc int[4] { 1, 3, 6, 9 };

    Pointer<uint> uptr = ptr.As<uint>();
}
```

> [!IMPORTANT]
> Typed pointer doesn't provide range checks.

Volatile operations are fully supported by pointer of one of the following types: `long`, `int`, `byte`, `bool`, `float`, `double`, `ulong`, `uint`, `sbyte`:
```csharp
using DotNext.Runtime.InteropServices;
using DotNext.Threading;

Pointer<long> ptr;
ptr.VolatileWrite(42L);
var i = ptr.VolatileRead();
i = ptr.IncrementAndGetValue(); //i == 43
```
See [here](../../api/DotNext.Threading.AtomicPointer.yml) for complete list of volatile operations.