Alloc vs Rental
=====
.NET allows to rent a memory instead of allocation using **new** keyword. It is useful in many cases especially when you need a large block of memory or large array. There a many articles describing benefits of this approach.
* [Pooling large arrays with ArrayPool](https://adamsitnik.com/Array-Pool/)
* [Avoid GC Pressure](https://michaelscodingspot.com/avoid-gc-pressure/)

The memory can be rented using [ArrayPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) or [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1) but their direct usage has several inconveniences:
* Not possible to use **using** statement to return rented array back to the pool in case of `ArrayPool<T>`
* It's hard to mix the code when rental is optional. For instance, in case of small block of memory you can use **stackalloc** instead of renting memory
* The returned memory or array can have larger size so you need to control bounds by yourself

.NEXT offers convenient wrappers that simplify the rental process and handle situations when renting is optional:
* [ArrayRental&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.ArrayRental-1.html) if you need to work with arrays
* [MemoryRental&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.ArrayRental-1.html) if you need to work with [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1)

# ArrayRental
[ArrayRental&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.ArrayRental-1.html) allows to rent the array using array pool and supports **using** statement.
```csharp
using DotNext.Buffers;

using(var array = new ArrayRental<byte>(10))
{
  Memory<byte> mem = array.Memory;
  Span<byte> span = array.Span;
  ArraySegment<byte> segment = array.Segment;
}

//the code is equivalent to
using System.Buffers;

var array = ArrayPool<byte>.Shared.Rent(10);
try
{
}
finally
{
  Array<byte>.Shared.Return(array);
}
```
`ArrayRental` provides several accessor to the rented array using `Memory`, `Span` and `Segment` properties. All these properties return the representation of the rented array with exact size that was initially requested. Additionally, it is possible to obtain direct reference to the rented array using explicit cast operator, e.g. `(byte[])array`.

The type supports custom array pool that can be passed to the constructor. In some advanced scenarios, you may have already allocated array so you don't to rent a new one from the pool. It is possible to pass such array as an argument of `ArrayRental` constructor.

# MemoryRental
[MemoryRental&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.ArrayRental-1.html) is more specialized version of `ArrayRental` which is useful in hybrid scenarios when renting can be replaced with stack allocation. This type is **ref**-like value type so it cannot be stored in fields or used inside of **async** methods. The rented memory is only accessible using [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type.

The following example demonstrates how to reverse a string and choose temporary buffers allocation method depending on the size of the string.
```csharp
using DotNext.Buffers;

public static unsafe string Reverse(this string str)
{
  if (str.Length == 0) return str;
  MemoryRental<char> result = str.Length <= 1024 ? stackalloc char[str.Length] : new MemoryRental<char>(str.Length);
  try
  {
    str.AsSpan().CopyTo(result.Span);
    result.Span.Reverse();
    fixed (char* ptr = result.Span)
      return new string(ptr, 0, result.Length);
  }
  finally
  {
    result.Dispose();
  }
} 
```
In constrast to `ArrayRental<T>`, this type uses [MemoryPool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1). It is possible to pass custom memory pool the constructor.

The type is typically used in unsafe context when you need a temporary buffer to perform in-memory transformations. If you don't have intentions to use **stackalloc** then choose `ArrayRental<T>` instead.
