Low-Level Memory Manipulations
====
.NEXT library contains [Memory](../../api/DotNext.Runtime.InteropServices.Memory.yml) class that extends capabilities of [Unsafe](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe) class from .NET Standard library.

> [!IMPORTANT]
> Most of the provided methods are not verifiable in terms of CLR Validation and Verification and can destabilize runtime. Correctness of their usage must be guaranteed by developer. For more information, read [II.3 Validation and Verification](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf) section of ECMA-335 standard.

Functionality of [Memory](../../api/DotNext.Runtime.InteropServices.Memory.yml) class is grouped into the following blocks:
1. Memory read operations
1. Memory write operations
1. Memory equality and comparison
1. Utility methods to work with [typedref](https://docs.microsoft.com/en-us/dotnet/api/system.typedreference) special type
1. Moving memory content
1. Utilities to work with managed pointers (typed `T&`)

All these features allow to manipulate managed and unmanaged memory to achieve the best perfomance. Regular business applications usually don't need such functionality.

The following example demonstrates how to compare two unmanaged blocks of memory:
```csharp
using DotNext.Runtime.InteropServices;
using System.Runtime.InteropServices;

const int memorySize = 1024;
var ptr1 = Marshal.AllocHGlobal(memorySize);
var ptr2 = Marshal.AllocHGlobal(memorySize);

Memory.Equals(ptr1, ptr2);
```

Most of these methods don't distinguish managed and unmanaged memory regions. It is responsibility of the caller code to pin the block of managed memory to avoid memory reallocations caused by Garbage Collector.

The following example demonstrates how to check whether the managed pointer is **null**:
```csharp
using DotNext.Runtime.InteropServices;
using System.Runtime.CompilerServices;

var nullref = Unsafe.AsRef<byte>(Memory.NullPtr);
Memory.IsNull(in nullref);
```