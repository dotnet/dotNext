Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Host | .NET Core 2.2.6 (CoreCLR 4.6.27817.03, CoreFX 4.6.27818.02), 64bit RyuJIT |
| Job | .NET Core 2.2.6 (CoreCLR 4.6.27817.03, CoreFX 4.6.27818.02), 64bit RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 18.04.2 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logical Cores | 8 |
| RAM | 24 GB |

# Bitwise Equality
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseEqualityBenchmark.cs) compares performance of [ValueType&lt;T&gt;.BitwiseEquals](./api/DotNext.ValueType-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `ValueType<Guid>.BitwiseEquals` | 5.104 ns | 0.1364 ns | 0.2459 ns |
| `Guid.Equals` | 12.320 ns | 0.2798 ns | 0.4101 ns |
| `ReadOnlySpan.SequenceEqual` for `Guid` | 12.981 ns | 0.2523 ns | 0.2360 ns |
| `ValueType<LargeStruct>.BitwiseEquals` | 22.542 ns | 0.4742 ns | 0.5461 ns |
| `LargeStruct.Equals` | 53.299 ns | 0.8754 ns | 0.7760 ns |
| `ReadOnlySpan.SequenceEqual` for `LargeStruct` | 24.184 ns | 0.6122 ns | 0.8973 ns |

Bitwise equality method has the better performance than field-by-field equality check because `BitwiseEquals` utilizes low-level optimizations performed by .NET Core according with underlying hardware such as SIMD. Additionally, it uses [aligned memory access](https://en.wikipedia.org/wiki/Data_structure_alignment) in constrast to [SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal) method.

# Equality of Arrays
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/ArrayEqualityBenchmark.cs) compares performance of [OneDimensionalArray.SequenceEqual](./api/DotNext.OneDimensionalArray.yml), [OneDimensionalArray.BitwiseEquals](./api/DotNext.OneDimensionalArray.yml) and manual equality check between two arrays using `for` loop. The benchmark is applied to the array of [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) elements.

`SequenceEqual` requires that array element type should implement [IEquatable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1) interface and calls `Equals(T other)` for each element.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid[].BitwiseEquals`, small arrays (~10 elements) | 9.758 ns | 0.0195 ns | 0.0163 ns |
| `Guid[].SequenceEqual`, small arrays (~10 elements) | 31.963 ns | 0.3506 ns | 0.3279 ns |
| `for` loop, small arrays (~10 elements) | 58.506 ns | 0.1090 ns | 0.0910 ns |
| `Guid[].BitwiseEquals`, large arrays (~100 elements) | 45.525 ns | 0.1935 ns | 0.1715 ns |
| `Guid[].SequenceEqual`, large arrays (~100 elements) | 309.409 ns | 6.0189 ns | 7.3917 ns |
| `for` loop, large arrays (~100 elements) | 727.733 ns | 14.5560 ns | 23.9160 ns |

`BitwiseEquals` is an absolute winner for equality check between arrays of any size.

# Bitwise Hash Code
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [ValueType&lt;T&gt;.BitwiseHashCode](./api/DotNext.ValueType-1.yml) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid.GetHashCode` | 2.704 ns | 0.0207 ns | 0.0183 ns |
| `ValueType<Guid>.BitwiseHashCode` | 8.798 ns | 0.0854 ns | 0.0757 ns |
| `ValueType<BigStructure>.BitwiseHashCode` | 23.087 ns | 0.1273 ns | 0.1191 ns |
| `BigStructure.GetHashCode` | 49.613 ns | 0.1804 ns | 0.1506 ns |

`BitwiseHashCode` is very efficient for hashing of large value types.

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

# Volatile Access to Arbitrary Value Type
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Atomic | 1.194 ms | 0.0245 ms | 0.2223 ms | 1.144 ms |
| Synchronized | 1.502 ms | 0.288 ms | 0.2623 ms | 1.451 ms |

# Value Delegate
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/FunctionPointerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | --- |
| Instance method, regular delegate, has implicit **this** | 2.747 ns | 0.0379 ns | 0.0336 ns | 2.741 ns |
| Instance method, Value Delegate, has implicit **this** | 4.108 ns | 0.0652 ns | 0.0578 ns | 4.101 ns |
| Static method, regular delegate, large size of param type, no implicitly captured object | 6.590 ns | 0.3193 ns | 0.9366 ns | 6.435 ns |
| Static method, Value Delegate, large size of param type, no implicitly captured object | 17.790 ns | 0.5467 ns | 1.0268 ns | 17.582 ns |
| Static method, regular delegate, small size of param type, no implicitly captured object | 85.961 ns | 1.7877 ns | 4.1076 ns | 84.270 ns |
| Static method, Value Delegate, small size of param type, no implicitly captured object | 80.220 ns | 0.7253 ns | 0.6430 ns | 80.184 ns |

_Large size of param type_ means that the type of the parameter is larger than 64 bit.

Interpretation of benchmark results:
* _Proxy_ mode of Value Delegate adds a small overhead in comparison with regular delegate
* If the type of the parameter is less than or equal to the size of CPU register then Value Delegate offers the best performance
* If the type of the parameter is greater than the size of CPU register then Value Delegate is slower than regular delegate
