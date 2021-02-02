Intrinsics
====
Intrinsic methods are special kind of methods whose implementation is handled specially by JIT compiler or written in pure IL code to achieve low (or zero, in some situations) overhead. .NET library has good example in the form of [Unsafe](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe) class which is implemented in pure IL. The implementation in pure IL allows to be closer to the bare metal and utilize code patterns recognized by JIT compiler. 

.NEXT library has numerous intrinsics which are exposed by [Intrinsics](xref:DotNext.Runtime.Intrinsics) class and grouped into the following categories:
* Common intrinsics which have very low or zero runtime overhead
* Low-level memory manipulations

# Common Intrinsics
Common intrinsics are various methods aimed to increase performance in some situations.

`Bitcast` method provides fast converison between two value types. This method is described in details in [this](./valuetype.md) article.

`IsNullable` method is a macros that allows to check whether the type `T` is nullable type or not. Typically, the method call can be replaced by constant value of type **bool** by JIT compiler so you get zero runtime overhead.

`IsDefault` method is the most useful method that allows to check whether the value of type `T` is `default(T)`. It works for both value type and reference type.

```csharp
using DotNext.Runtime;

Intrinsics.IsDefault("");   //false
Intrinsics.IsDefault(default(string));  //true
Intrinsics.IsDefault(0);    //true
Intrinsics.IsDefault(1);    //false
```

# Low-Level Memory Manipulations
Intrinsic methods for low-level manipulations extend capabilities of [Unsafe](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe) class from .NET standard library.

> [!IMPORTANT]
> Most of the provided methods are not verifiable in terms of CLR Validation and Verification and can destabilize runtime. Correctness of their usage must be guaranteed by developer. For more information, read [II.3 Validation and Verification](https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf) section of ECMA-335 standard.

Memory manipulation methods of [Intrinsics](xref:DotNext.Runtime.Intrinsics) class is grouped into the following blocks:
1. Hash computation
1. Memory equality and comparison
1. Utility methods to work with [typedref](https://docs.microsoft.com/en-us/dotnet/api/system.typedreference) special type
1. Direct memory manipulations: copying, reversing, clearing bits etc.
1. Utilities to work with managed pointers (type `T&`)

All these features allow to manipulate managed and unmanaged memory to achieve the best perfomance. Regular business applications usually don't need such functionality.

The following example demonstrates how to compare two unmanaged blocks of memory:
```csharp
using DotNext.Runtime;
using System.Runtime.InteropServices;

const int memorySize = 1024;
var ptr1 = Marshal.AllocHGlobal(memorySize);
var ptr2 = Marshal.AllocHGlobal(memorySize);

Intrinsics.Equals(ptr1.ToPointer(), ptr2.ToPointer(), memorySize);
```

Most of these methods don't distinguish managed and unmanaged memory regions. It is responsibility of the caller code to pin the block of managed memory to avoid memory reallocations caused by Garbage Collector.

The following example demonstrates how to check whether the managed pointer is **null**:
```csharp
using DotNext.Runtime;
using System.Runtime.CompilerServices;

var nullref = Unsafe.AsRef<byte>(default(void*));
Intrinsics.IsNull(in nullref);
```

# Exact Type-Testing
The **is** operator in C# checks if the result of an expression is compatible with a given type. However, in some rare cases you need to check whether the object is of exact type. `IsExactTypeOf` method provides optimized implementation of this case:
```csharp
"a" is object;  //true
"a" is string;  //true
Intrinsics.IsExactTypeOf<object>("a");  //false
Intrinsics.IsExactTypeOf<string>("a");  //true
```

# Array Length
C# 9 introduces primitive type `nint` that can be used for accessing array elements without hidden conversion of the index. However, there is no way to obtain array length as native integer. `Intrinsics.GetLength` provides this ability so the loop over array elements can be rewritten as follows:
```csharp
using DotNext.Runtime;

var array = new int[] { ... };
for (nint i = 0; i < Intrinsics.GetLength(array); i++)
{
    
}
```