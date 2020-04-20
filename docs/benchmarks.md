Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Host | .NET Core 3.1.3 (CoreCLR 4.700.20.11803, CoreFX 4.700.20.12001), X64 RyuJIT |
| Job | .NET Core 3.1.3 (CoreCLR 4.700.20.11803, CoreFX 4.700.20.12001), X64 RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 18.04.4 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logical Cores | 8 |
| RAM | 24 GB |

You can run benchmarks using `Bench` build configuration as follows:
```bash
cd <dotnext-clone-path>/src/DotNext.Benchmarks
dotnet run -c Bench
```

# Bitwise Equality
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseEqualityBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.Equals](./api/DotNext.BitwiseComparer-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `BitwiseComparer<Guid>.Equals` |  3.3381 ns | 0.0323 ns | 0.0287 ns |
| `Guid.Equals` |  2.1275 ns | 0.0126 ns | 0.0112 ns |
| `ReadOnlySpan.SequenceEqual` for `Guid`  | 4.5505 ns | 0.0477 ns | 0.0423 ns
| `BitwiseComparer<LargeStruct>.Equals` | 16.0034 ns | 0.3494 ns | 0.5542 ns |
| `LargeStruct.Equals` | 45.1184 ns | 0.0946 ns | 0.0884 ns |
| `ReadOnlySpan.SequenceEqual` for `LargeStruct` | 24.8373 ns | 0.4922 ns | 0.4364 ns |

Bitwise equality method has the better performance than field-by-field equality check especially for large value types because `BitwiseEquals` utilizes low-level optimizations performed by .NET Core according with underlying hardware such as SIMD. Additionally, it uses [aligned memory access](https://en.wikipedia.org/wiki/Data_structure_alignment) in constrast to [SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal) method.

# Equality of Arrays
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/ArrayEqualityBenchmark.cs) compares performance of [ReadOnlySpan.SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal#System_MemoryExtensions_SequenceEqual__1_System_ReadOnlySpan___0__System_ReadOnlySpan___0__), [OneDimensionalArray.BitwiseEquals](./api/DotNext.OneDimensionalArray.yml) and manual equality check between two arrays using `for` loop. The benchmark is applied to the array of [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) elements.

| Method | Mean | Error | StdDev | Median  |
| ---- | ---- | ---- | ---- | ---- |
| `Guid[].BitwiseEquals`, small arrays (~10 elements) | 9.039 ns | 0.0426 ns |  0.0399 ns | 9.042 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, small arrays (~10 elements) | 45.571 ns |  0.1440 ns |  0.1276 ns | 45.569 ns |
| `for` loop, small arrays (~10 elements) | 63.031 ns | 0.1057 ns | 0.0937 ns | 63.064 ns |
| `Guid[].BitwiseEquals`, large arrays (~100 elements) | 47.892 ns |  0.2260 ns | 0.2004 ns | 47.893 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, large arrays (~100 elements) | 381.872 ns | 7.4653 ns | 11.1737 ns | 375.948 ns |
| `for` loop, large arrays (~100 elements) | 631.655 ns | 12.4212 ns | 19.7013 ns | 634.035 ns |

Bitwise equality is an absolute winner for equality check between arrays of any size.

# Bitwise Hash Code
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.GetHashCode](./api/DotNext.BitwiseComparer-1.yml) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| `Guid.GetHashCode` |  1.578 ns | 0.0328 ns | 0.0540 ns | 1.570 ns |
| `BitwiseComparer<Guid>.GetHashCode` | 7.288 ns | 0.1816 ns | 0.3084 ns | 7.454 ns |
| `BitwiseComparer<LargeStructure>.GetHashCode` | 40.471 ns | 0.1063 ns | 0.0994 ns | 40.445 ns |
| `LargeStructure.GetHashCode` | 27.826 ns | 0.4569 ns | 0.5611 ns | 27.759 ns |

Bitwise hash code algorithm is slower than JIT optimizations introduced by .NET Core 3.1 but still convenient in complex cases.

# Bytes to Hex
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/HexConversionBenchmark.cs) demonstrates performance of `DotNext.Span.ToHex` extension method that allows to convert arbitrary set of bytes into hexadecimal form. It is compatible with`Span<T>` data type in constrast to [BitConverter.ToString](https://docs.microsoft.com/en-us/dotnet/api/system.bitconverter.tostring) method.

| Method | Num of Bytes | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- | ---- |
| `BitConverter.ToString` | 16 bytes | 70.23 ns | 0.448 ns | 0.419 ns |
| `Span.ToHex` | 16 bytes | 71.96 ns | 0.507 ns | 0.450 ns |
| `BitConverter.ToString` | 64 bytes | 242.70 ns | 1.423 ns | 1.111 ns |
| `Span.ToHex` | 64 bytes | 151.06 ns | 2.869 ns | 3.730 ns |
| `BitConverter.ToString` | 128 bytes | 481.28 ns | 1.176 ns | 1.042 ns |
| `Span.ToHex` | 128 bytes | 253.64 ns | 5.092 ns | 6.622 ns |
| `BitConverter.ToString` | 256 bytes | 936.35 ns | 18.775 ns | 23.745 ns |
| `Span.ToHex` | 256 bytes | 467.52 ns | 1.321 ns | 1.103 ns |

`Span.ToHex` demonstrates the best performance especially for large arrays.

# Fast Reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/PropertyGetterReflectionBenchmark.cs) demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Direct call | 11.41 ns | 0.080 ns | 0.067 ns | 11.42 ns |
| Reflection with DotNext using delegate type `MemberGetter<IndexOfCalculator, int>` | 12.71 ns | 0.196 ns | 0.184 ns | 12.64 ns |
| Reflection with DotNext using `DynamicInvoker` | 21.99 ns | 0.046 ns | 0.043 ns | 21.98 ns |
| Reflection with DotNext using delegate type `Function<object, ValueTuple, object>` | 21.98 ns | 0.072 ns | 0.060 ns | 21.97 ns |
| `ObjectAccess` class from _FastMember_ library | 52.67 ns | 0.120 ns | 0.112 ns | 52.67 ns |
| .NET reflection | 52.67 ns | 0.120 ns | 0.112 ns | 52.67 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

## Instance Method Call
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/StringMethodReflectionBenchmark.cs) demonstrates overhead of calling instance method `IndexOf` of type **string** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf))`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<object, (object, object), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

The benchmark uses series of different strings to run the same set of tests. Worst case means that character lookup is performed for a string that doesn't contain the given character. Best case means that character lookup is performed for a string that has the given character.

| Method | Condition | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- | ---- |
| Direct call | Empty String | 6.321 ns | 0.1605 ns | 0.1784 ns |
| Direct call | Best Case | 9.329 ns | 0.0867 ns | 0.0724 ns |
| Direct call | Worst Case | 12.657 ns | 0.1746 ns | 0.1633 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Empty String | 8.017 ns | 0.0424 ns | 0.0331 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Best Case | 12.418 ns | 0.0512 ns | 0.0479 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Worst Case | 18.113 ns | 0.0588 ns | 0.0550 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Empty String | 12.389 ns | 0.0227 ns | 0.0202 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Best Case | 17.252 ns | 0.0729 ns | 0.0682 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Worst Case | 20.930 ns | 0.0226 ns | 0.0200 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Empty String | 30.871 ns | 0.1307 ns | 0.1222 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Best Case | 33.069 ns | 0.5989 ns | 1.0006 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Worst Case | 35.981 ns | 0.7064 ns | 0.6938 ns |
| .NET reflection | Empty String | 311.787 ns | 1.0205 ns | 0.9546 ns |
| .NET reflection | Best Case | 318.686 ns | 6.2622 ns | 6.4308 ns |
| .NET reflection | Worst Case | 332.189 ns | 1.3130 ns | 1.2282 ns |

DotNext Reflection library offers the best result in case when delegate type exactly matches to the reflected method with small overhead measured in a few nanoseconds.

## Static Method Call
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/TryParseReflectionBenchmark.cs) demonstrates overhead of calling static method `TryParse` of type **decimal** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static)`. The delegate type exactly matches to the reflected method signature: `delegate bool TryParseDelegate(string text, out decimal result)`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(string text, decimal result), bool>`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(object text, object result), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Direct call | 124.1 ns | 1.98 ns | 2.78 ns |
| Reflection with DotNext using delegate type `TryParseDelegate` | 133.5 ns | 0.28 ns | 0.25 ns |
| Reflection with DotNext using delegate type `Function<(string text, decimal result), bool>` | 142.8 ns | 0.23 ns | 0.21 ns |
| Reflection with DotNext using delegate type `Function<(object text, object result), object>` | 182.4 ns | 0.77 ns | 0.68 ns |
| .NET reflection | 517.0 ns | 1.53 ns | 1.43 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Atomic | 359.6 us | 7.40 us | 69.64 us | 353.2 us |
| Synchronized | 947.2 us | 10.30 us | 96.05 us | 945.1 us |
| SpinLock | 1,707.6 us | 52.27 us | 488.34 us | 1,674.8 us |

# Value Delegate
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/FunctionPointerBenchmark.cs) compares performance of indirect method call using classic delegates from .NET and [value delegates](./features/core/valued.md).

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Instance method, regular delegate, has implicit **this** | 0.9273 ns | 0.0072 ns | 0.0060 ns |
| Instance method, Value Delegate, has implicit **this** | 1.8824 ns | 0.0495 ns | 0.0463 ns |
| Static method, regular delegate, large size of param type, no implicitly captured object | 14.5560 ns | 0.0440 ns | 0.0367 ns |
| Static method, Value Delegate, large size of param type, no implicitly captured object | 15.7549 ns | 0.0731 ns | 0.0684 ns |
| Static method, regular delegate, small size of param type, no implicitly captured object | 23.2037 ns | 0.3844 ns | 0.3408 ns |
| Static method, Value Delegate, small size of param type, no implicitly captured object | 21.8213 ns | 0.1073 ns | 0.0896 ns |

_Large size of param type_ means that the type of the parameter is larger than 64 bit.

Interpretation of benchmark results:
* _Proxy_ mode of Value Delegate adds a small overhead in comparison with regular delegate
* If the type of the parameter is less than or equal to the size of CPU register then Value Delegate offers the best performance
* If the type of the parameter is greater than the size of CPU register then Value Delegate is slower than regular delegate
