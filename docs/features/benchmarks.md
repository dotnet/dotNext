Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:
| Parameter | Configuration |
| ---- | ---- |
| Host | .NET Core 2.2.3 (CoreCLR 4.6.27414.05, CoreFX 4.6.27414.05), 64bit RyuJIT |
| Job | .NET Core 2.2.3 (CoreCLR 4.6.27414.05, CoreFX 4.6.27414.05), 64bit RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 18.04 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logiccal Cores | 8 |
| RAM | 24 GB |

# Bitwise Equality
This benchmark compares performance of [ValueType&lt;T&gt;.BitwiseEquals](../api/DotNext.ValueType-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `ValueType<Guid>.BitwiseEquals` | 9.627 ns | 0.2266 ns | 0.3461 ns |
| `Guid.Equals` | 12.320 ns | 0.2798 ns | 0.4101 ns |
| `ValueType<BigStruct>.BitwiseEquals` | 27.097 ns | 0.5794 ns | 1.2221 ns |
| `BigStruct.Equals` | 53.299 ns | 0.8754 ns | 0.7760 ns |

Bitwise equality method has the better performance than field-by-field equality check because `BitwiseEquals` utilizes low-level optimizations performed by .NET Core according with underlying hardware such as SIMD.

# Array Equality
This benchmark compares performance of [OneDimensionalArray.SequenceEqual](../api/DotNext.OneDimensionalArray.yml), [OneDimensionalArray.BitwiseEquals](../api/DotNext.OneDimensionalArray.yml) and manual equality check between two arrays using `for` loop. The benchmark is applied to the array of [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) elements.

`SequenceEqual` requires that array element type should implement [IEquatable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1) interface and calls `Equals(T other)` for each element.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid[].BitwiseEquals`, small arrays (~10 elements) | 13.73 ns | 0.3562 ns | 0.3157 ns |
| `Guid[].SequenceEqual`, small arrays (~10 elements) | 34.57 ns | 0.6698 ns | 0.5593 ns |
| `for` loop, small arrays (~10 elements) | 59.13 ns | 1.1550 ns | 1.2358 ns | 
| `Guid[].BitwiseEquals`, large arrays (~100 elements) |  46.78 ns | 0.9868 ns | 1.2480 ns |
| `Guid[].SequenceEqual`, large arrays (~100 elements) | 307.99 ns | 1.9631 ns | 1.6393 ns |
| `for` loop, large arrays (~100 elements) | 610.53 ns | 9.2729 ns | 8.2202 ns |

`BtiwiseEquals` is an absolute winner for equality check between large arrays.

# Bitwise Hash Code
This benchmark compares performance of [ValueType&lt;T&gt;.BitwiseHashCode](../api/DotNext.ValueType-1.yml) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid.GetHashCode` | 2.704 ns | 0.0207 ns | 0.0183 ns |
| `ValueType<Guid>.BitwiseHashCode` | 8.798 ns | 0.0854 ns | 0.0757 ns |
| `ValueType<BigStructure>.BitwiseHashCode` | 23.087 ns | 0.1273 ns | 0.1191 ns |
| `BigStructure.GetHashCode` | 49.613 ns | 0.1804 ns | 0.1506 ns |

`BitwiseHashCode` is very efficient for hashing of large value types.

# Strongly typed reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
This benchmark demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Direct call | 11.52 ns | 0.2284 ns | 0.5109 ns |
| Reflection with DotNext using delegate type `MemberGetter<IndexOfCalculator, int>` | 11.58 ns | 0.2291 ns | 0.3136 ns |
| Reflection with DotNext using delegate type `Function<object, ValueTuple, object>` | 19.12 ns | 0.3870 ns | 0.9919 ns |
| `ObjectAccess` class from _FastMember_ library |  19.12 ns | 0.3870 ns | 0.9919 ns |
| .NET reflection | 157.71 ns | 3.1092 ns | 4.5574 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

## Instance Method Call
This benchmarks demonstrates overhead of calling instance method `IndexOf` of type **string** caused by different mechanisms:
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

# Static Method Call
This benchmarks demonstrates overhead of calling static method `TryParse` of type **decimal** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static)`. The delegate type exactly matches to the reflected method signature: `delegate bool TryParseDelegate(string text, out decimal result)`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(string text, decimal result), bool>`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(object text, object result), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Direct call | 169.7 ns | 3.3878 ns | 5.6602 ns | 168.5 ns |
| Reflection with DotNext using delegate type `TryParseDelegate` | 167.8 ns |  3.3781 ns | 6.0045 ns | 166.1 ns |
| Reflection with DotNext using delegate type `Function<(string text, decimal result), bool>` | 174.5 ns |  1.7939 ns | 1.6780 ns | 174.1 ns |
| Reflection with DotNext using delegate type `Function<(object text, object result), object>` | 191.5 ns |  0.5836 ns | 0.5459 ns | 191.6 ns |
| .NET reflection | 625.4 ns | 11.3603 ns | 9.4864 ns | 626.2 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.