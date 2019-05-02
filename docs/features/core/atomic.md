Atomic Operations
====

Most .NET programming languages provide primitive atomic operations to work with fields with concurrent access. For example, C# [volatile](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile) keyword is a language feature for atomic read/write of the marked field. But what if more complex atomic operation is required? Java provides [such features](https://docs.oracle.com/javase/8/docs/api/java/util/concurrent/atomic/AtomicInteger.html) at library level, with some overhead associated with object allocation. C# and many other .NET languages support concept of [passing by refence](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref) so it is possible to obtain a reference to the field value. This ability allows to avoid overhead of atomic primitives typical to JVM languages. Moreover, extension methods may accept **this** parameter by reference forming the foundation for atomic operations provided  by .NEXT library.

The library provides advanced atomic operations for the following types:
* Scalar types
    * [long](https://docs.microsoft.com/en-us/dotnet/api/system.int64)
    * [int](https://docs.microsoft.com/en-us/dotnet/api/system.int32)
    * [bool](https://docs.microsoft.com/en-us/dotnet/api/system.boolean)
    * [double](https://docs.microsoft.com/en-us/dotnet/api/system.double)
    * [float](https://docs.microsoft.com/en-us/dotnet/api/system.single)
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
All atomic operations are extension methods grouped by specific target scalar types:
* [AtomicInteger](../../api/DotNext.Threading.AtomicInt32.yml) for [int](https://docs.microsoft.com/en-us/dotnet/api/system.int32)
* [AtomicLong](../../api/DotNext.Threading.AtomicInt64.yml) for [long](https://docs.microsoft.com/en-us/dotnet/api/system.int64)
* [AtomicFloat](../../api/DotNext.Threading.AtomicSingle.yml) for [float](https://docs.microsoft.com/en-us/dotnet/api/system.single)
* [AtomicDouble](../../api/DotNext.Threading.AtomicDouble.yml) for [double](https://docs.microsoft.com/en-us/dotnet/api/system.double)
* [AtomicReference](../../api/DotNext.Threading.AtomicReference.yml) for [reference types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/reference-types)

One exception is [bool](https://docs.microsoft.com/en-us/dotnet/api/system.boolean) data type. The only way to use atomic boolean operations is to replace this data type with [AtomicBoolean](../../api/DotNext.Threading.AtomicBoolean.yml) type.

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