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
	* [UIntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.uintptr)
* One-dimensional arrays

Numeric types have the following atomic operations:
* _AccumulateAndGet_, _GetAndAccumulate_ - atomic modification of the field where modification logic is based on the supplied value and custom accumulator binary function
* _UpdateAndGet_, _GetAndUpdate_ - atomic modification of the field where modification logic is based in the custom unary function

Reference types have similar set of atomic operations except arithmetic operations such as increment, decrement and addition.

# Atomic operations for scalar types
Atomic operations are extension methods exposed by [AtomicInt32](xref:DotNext.Threading.Atomic) class.

Atomic operations for some data types represented by atomic containers instread of extension methods:
* [Atomic.Boolean](xref:DotNext.Threading.Atomic.Boolean) for [bool](https://docs.microsoft.com/en-us/dotnet/api/system.boolean) data type

The following example demonstrates how to use advanced atomic operations
```csharp
using DotNext.Threading;

public class TestClass
{
    private long field;

    public void IncByTwo() => field.UpdateAndGet(x => x + 2);   //update field with a sum of its value and constant 2 atomically

    public long Sub(long value) => field.AccumulateAndGet(value, (current, v) => current - value); //the same as field -= value but performed atomically
}
```

# Atomic access for arbitrary value types
Volatile memory access is hardware dependent feature. For instance, on x86 atomic read/write can be guaranteed for 32-bit data types only. On x86_64, this guarantee is extended to 64-bit data type. What if you need to have hardware-independent atomic read/write for arbitrary value type? The naive solution is to use [Synchronized](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.methodimploptions#System_Runtime_CompilerServices_MethodImplOptions_Synchronized) method. It can be declared in class only, not in value type. If your volatile field declared in value type then you cannot use such kind of methods or you need to create container in the form of the class which requires allocation on the heap.

[Atomic&lt;T&gt;](xref:DotNext.Threading.Atomic`1) is a container that provides atomic operations for arbitrary value type. The container is value type itself and do not require heap allocation. Memory access to the stored value is organized through software-emulated memory barrier which is portable across CPU architectures. Performance impact is very low. Under heavy lock contention, the access time is ~20-30% faster than Synchronized methods. Check [Benchmarks](../../benchmarks.md) for information.

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