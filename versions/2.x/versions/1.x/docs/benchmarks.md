Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Host | .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT |
| Job | .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 18.04.2 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logical Cores | 8 |
| RAM | 24 GB |

# Bitwise Equality
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseEqualityBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.Equals](./api/DotNext.BitwiseComparer-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| `BitwiseComparer<Guid>.Equals` | 8.327 ns | 0.0260 ns | 0.0243 ns |  8.326 ns |
| `Guid.Equals` | 12.155 ns | 0.0492 ns | 0.0411 ns | 12.152 ns |
| `ReadOnlySpan.SequenceEqual` for `Guid` | 12.680 ns | 0.1956 ns | 0.1830 ns | 12.625 ns |
| `BitwiseComparer<LargeStruct>.Equals` | 14.243 ns | 0.3126 ns | 0.4582 ns | 14.000 ns |
| `LargeStruct.Equals` | 50.952 ns | 0.3261 ns | 0.3050 ns | 50.936 ns |
| `ReadOnlySpan.SequenceEqual` for `LargeStruct` | 35.171 ns | 0.2531 ns | 0.2244 ns | 35.110 ns |

Bitwise equality method has the better performance than field-by-field equality check because `BitwiseEquals` utilizes low-level optimizations performed by .NET Core according with underlying hardware such as SIMD. Additionally, it uses [aligned memory access](https://en.wikipedia.org/wiki/Data_structure_alignment) in constrast to [SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal) method.

# Equality of Arrays
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/ArrayEqualityBenchmark.cs) compares performance of [ReadOnlySpan.SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal#System_MemoryExtensions_SequenceEqual__1_System_ReadOnlySpan___0__System_ReadOnlySpan___0__), [OneDimensionalArray.BitwiseEquals](./api/DotNext.OneDimensionalArray.yml) and manual equality check between two arrays using `for` loop. The benchmark is applied to the array of [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) elements.

`SequenceEqual` requires that array element type should implement [IEquatable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1) interface and calls `Equals(T other)` for each element.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid[].BitwiseEquals`, small arrays (~10 elements) |  10.66 ns | 0.2459 ns |  0.5397 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, small arrays (~10 elements) |  34.89 ns | 0.6313 ns |  0.5272 ns |
| `for` loop, small arrays (~10 elements) |  65.20 ns | 1.3086 ns |  1.2853 ns |
| `Guid[].BitwiseEquals`, large arrays (~100 elements) |  50.82 ns | 1.0471 ns |  1.9408 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, large arrays (~100 elements) | 339.25 ns | 6.6708 ns | 12.8525 ns |
| `for` loop, large arrays (~100 elements) | 584.56 ns | 4.2687 ns |  3.9930 ns |

Bitwise equality is an absolute winner for equality check between arrays of any size.

# Bitwise Hash Code
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.GetHashCode](./api/DotNext.BitwiseComparer-1.yml) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| `Guid.GetHashCode` |  2.637 ns | 0.0121 ns | 0.0101 ns |
| `BitwiseComparer<Guid>.GetHashCode` |  6.924 ns | 0.0240 ns | 0.0225 ns |
| `BitwiseComparer<LargeStructure>.GetHashCode` | 23.206 ns | 0.4926 ns | 0.6743 ns |
| `LargeStructure.GetHashCode` | 51.915 ns | 0.0751 ns | 0.0666 ns |

Bitwise hash code algorithm is very efficient for hashing of large value types.

# Fast Reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/PropertyGetterReflectionBenchmark.cs) demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Direct call | 11.52 ns | 0.2284 ns | 0.5109 ns |
| Reflection with DotNext using delegate type `MemberGetter<IndexOfCalculator, int>` | 11.58 ns | 0.2291 ns | 0.3136 ns |
| Reflection with DotNext using delegate type `Function<object, ValueTuple, object>` | 19.12 ns | 0.3870 ns | 0.9919 ns |
| `ObjectAccess` class from _FastMember_ library |  50.25 ns | 0.3870 ns | 0.8247 ns |
| .NET reflection | 157.71 ns | 3.1092 ns | 4.5574 ns |

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
| Direct call | Empty String | 4.284 ns | 0.1163 ns | 0.1088 ns |
| Direct call | Best Case | 8.116 ns | 0.1956 ns | 0.2329 ns |
| Direct call | Worst Case | 13.866 ns | 0.3406 ns | 0.6141 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Empty String | 9.419 ns | 0.1792 ns | 0.2267 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Best Case | 13.120 ns | 0.2540 ns | 0.3802 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Worst Case | 18.270 ns | 0.3618 ns | 0.3384 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Empty String | 13.548 ns | 0.2691 ns | 0.3683 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Best Case | 17.618 ns | 0.3591 ns | 0.3687 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Worst Case | 23.436 ns | 0.3828 ns | 0.3581 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Empty String | 41.128 ns | 0.7754 ns | 1.3376 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Best Case | 40.432 ns | 0.7127 ns | 0.6318 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Worst Case | 51.208 ns | 0.3256 ns | 0.3046 ns |
| .NET reflection | Empty String | 335.322 ns | 7.5455 ns | 6.6889 ns |
| .NET reflection | Best Case | 327.107 ns | 1.3891 ns | 1.1600 ns |
| .NET reflection | Worst Case | 332.189 ns | 1.3130 ns | 1.2282 ns |

DotNext Reflection library offers the best result in case when delegate type exactly matches to the reflected method with small overhead measured in a few nanoseconds.

## Static Method Call
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/TryParseReflectionBenchmark.cs) demonstrates overhead of calling static method `TryParse` of type **decimal** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static)`. The delegate type exactly matches to the reflected method signature: `delegate bool TryParseDelegate(string text, out decimal result)`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(string text, decimal result), bool>`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(object text, object result), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Direct call | 169.7 ns | 3.3878 ns | 5.6602 ns | 168.5 ns |
| Reflection with DotNext using delegate type `TryParseDelegate` | 167.8 ns |  3.3781 ns | 6.0045 ns | 166.1 ns |
| Reflection with DotNext using delegate type `Function<(string text, decimal result), bool>` | 174.5 ns |  1.7939 ns | 1.6780 ns | 174.1 ns |
| Reflection with DotNext using delegate type `Function<(object text, object result), object>` | 191.5 ns |  0.5836 ns | 0.5459 ns | 191.6 ns |
| .NET reflection | 625.4 ns | 11.3603 ns | 9.4864 ns | 626.2 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Atomic | 494.2 us | 8.263 us | 71.95 us | 482.1 us |
| Synchronized | 938.1 us | 7.746 us | 72.56 us | 926.9 us |

# Value Delegate
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/FunctionPointerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | --- |
| Instance method, regular delegate, has implicit **this** |  3.042 ns | 0.0969 ns | 0.2842 ns |  2.880 ns |
| Instance method, Value Delegate, has implicit **this** |  4.209 ns | 0.1174 ns | 0.1793 ns |  4.243 ns |
| Static method, regular delegate, large size of param type, no implicitly captured object | 20.651 ns | 0.1441 ns | 0.1277 ns | 20.650 ns |
| Static method, Value Delegate, large size of param type, no implicitly captured object | 27.066 ns | 0.2038 ns | 0.1807 ns | 27.046 ns |
| Static method, regular delegate, small size of param type, no implicitly captured object | 78.562 ns | 0.4298 ns | 0.3810 ns | 78.526 ns |
| Static method, Value Delegate, small size of param type, no implicitly captured object | 68.681 ns | 1.0735 ns | 0.9517 ns | 68.466 ns |

_Large size of param type_ means that the type of the parameter is larger than 64 bit.

Interpretation of benchmark results:
* _Proxy_ mode of Value Delegate adds a small overhead in comparison with regular delegate
* If the type of the parameter is less than or equal to the size of CPU register then Value Delegate offers the best performance
* If the type of the parameter is greater than the size of CPU register then Value Delegate is slower than regular delegate
