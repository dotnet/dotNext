Atomic Operations
====

Most .NET programming languages provide primitive atomic operations to work with fields with concurrent access. For example, C# [volatile](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile) keyword is a language feature for atomic read/write of the marked field. But what if more complex atomic operation is required? Java provides [such features](https://docs.oracle.com/javase/8/docs/api/java/util/concurrent/atomic/AtomicInteger.html) at library level, with some overhead associated with object allocation. C# and many other .NET languages support concept of [passing by refence](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref) so it is possible to obtain a reference to the field value. This ability allows to avoid overhead of atomic primitives typical to JVM languages. Moreover, extension methods may accept **this** parameter by reference forming the foundation for atomic operations provided  by .NEXT library.

The library provides advanced atomic operations for the following types:
* Scalar types
    * [long](https://docs.microsoft.com/en-us/dotnet/api/system.int64)
	* [ulong](https://docs.microsoft.com/en-us/dotnet/api/system.uint64)
    * [int](https://docs.microsoft.com/en-us/dotnet/api/system.int32)
	* [uint](https://docs.microsoft.com/en-us/dotnet/api/system.uint32)
    * [bool](https://docs.microsoft.com/en-us/dotnet/api/system.boolean)
    * [double](https://docs.microsoft.com/en-us/dotnet/api/system.double)
    * [float](https://docs.microsoft.com/en-us/dotnet/api/system.single)
	* [IntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr)
    * [Reference types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/reference-types)
* One-dimensional arrays

Numeric types have the following atomic operations:
* _VolatileRead_
* _VolatileWrite_
* _IncrementAndGet_ - atomic increment of the field
* _DecrementAndGet_ - atomic decrement of the field
* _CompareAndSet_ - atomic modification of the field based on comparison
* _Add_ - atomic arithemtic addition
* _GetAndSet_, _SetAndGet_ - atomic modification of the field with ability to obtain modified value as a result
* _AccumulateAndGet_, _GetAndAccumulate_ - atomic modification of the field where modification logic is based on the supplied value and custom accumulator binary function
* _UpdateAndGet_, _GetAndUpdate_ - atomic modification of the field where modification logic is based in the custom unary function

Reference types have similar set of atomic operations except arithmetic operations such as increment, decrement and addition.

# Atomic operations for scalar types
Atomic operations are extension methods grouped by specific target scalar types:
* [AtomcInt32](../../api/DotNext.Threading.AtomicInt32.yml) for [int](https://docs.microsoft.com/en-us/dotnet/api/system.int32)
* [AtomcUInt32](../../api/DotNext.Threading.AtomicUInt32.yml) for [int](https://docs.microsoft.com/en-us/dotnet/api/system.uint32)
* [AtomicInt64](../../api/DotNext.Threading.AtomicInt64.yml) for [long](https://docs.microsoft.com/en-us/dotnet/api/system.int64)
* [AtomicUInt64](../../api/DotNext.Threading.AtomicUInt64.yml) for [long](https://docs.microsoft.com/en-us/dotnet/api/system.uint64)
* [AtomicSingle](../../api/DotNext.Threading.AtomicSingle.yml) for [float](https://docs.microsoft.com/en-us/dotnet/api/system.single)
* [AtomicDouble](../../api/DotNext.Threading.AtomicDouble.yml) for [double](https://docs.microsoft.com/en-us/dotnet/api/system.double)
* [AtomicIntPtr](../../api/DotNext.Threading.AtomicIntPtr.yml) for [IntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr)
* [AtomicReference](../../api/DotNext.Threading.AtomicReference.yml) for [reference types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/reference-types)

Atomic operations for some data types represented by atomic containers instread of extension methods:
* [AtomicBoolean](../../api/DotNext.Threading.AtomicBoolean.yml) for [bool](https://docs.microsoft.com/en-us/dotnet/api/system.boolean) data type
* [AtomicEnum](../../api/DotNext.Threading.AtomicEnum-1.yml) for **enum** data types

The following example demonstrates how to use advanced atomic operations
```csharp
using DotNext.Threading;

public class TestClass
{
    private long field;

    public void IncByTwo() => field.UpdateAndGet(x => x + 2);   //update field with a sum of its value and constant 2 atomically

    public void IncByTwo2() => field.Add(2);    //the same effect

    public long Sub(long value) => field.AccumulateAndGet(value, (current, v) => current - value); //the same as field -= value but performed atomically
}
```

# Atomic operations for arrays
C# doesn't provide volatile access to array elements syntactically in contrast with volatile fields. .NEXT library provides the same set of atomic operations as for scalar types with a small difference: array atomic operation accept element index as additional argument.

The second approach utilizes extension method.
```csharp
using DotNext.Threading;

var array = new double[10];
var result = array.IncrementAndGet(2);   //2 is an index of array element to be modified
result = array.VolatileRead(2);  //atomic read of array element
array.VolatileWrite(2, 30D);  //atomic modification of array element
```

# Atomic operations with pointers
Working with unmanaged memory in multithreaded application also requires atomic operations and volatile memory access. [AtomicPointer](../../api/DotNext.Threading.AtomicPointer.yml) provides all necessary functionality as extension methods for [Pointer&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.Pointer-1.yml) data type.

# Atomic access for arbitrary value types
Volatile memory access is hardware dependent feature. For instance, on x86 atomic read/write can be guaranteed for 32-bit data types only. On x86_64, this guarantee is extended to 64-bit data type. What if you need to have hardware-independent atomic read/write for arbitrary value type? The naive solution is to use [Synchronized](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.methodimploptions#System_Runtime_CompilerServices_MethodImplOptions_Synchronized) method. It can be declared in class only, not in value type. If your volatile field declared in value type then you cannot use such kind of methods or you need to create container in the form of the class which requires allocation on the heap.

[Atomic&lt;T&gt;](../../api/DotNext.Threading.Atomic-1.yml) is a container that provides atomic operations for arbitrary value type. The container is value type itself and do not require heap allocation. Memory access to the stored value is organized through software-emulated memory barrier which is portable across CPU architectures. Performance impact is very low. Under heavy lock contention, the access time is ~20-30% faster than Synchronized methods. Check [Benchmarks](../../benchmarks.md) for information.

The following example demonstrates how to organize atomic access to field of type [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid).
```csharp
using DotNext.Threading;

class MyClass
{
	private Atomic<Guid> id;

	public void GenerateNewId() => id.Write(Guid.NewGuid());	//Write is atomic

	public bool IsEmptyId 
	{
		get
		{
			id.Read(out var value);	//Read is atomic
			return value == Guid.Empty;
		}
	}
}
```