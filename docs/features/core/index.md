Base Class Library Enhancements
====

# Randomization
Related class: [RandomExtensions](xref:DotNext.RandomExtensions)

Extension methods for random data generation extends both classes [Random](https://docs.microsoft.com/en-us/dotnet/api/system.random) and [RandomNumberGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator).

## Random blittable values
Provides a way to generate random values of arbitrary types:
```csharp
using DotNext;
using System;
using System.Security.Cryptography;

var rand = new Random();
Int128 password = rand.Next<Int128>();
password = RandomNumberGenerator.Next<Int128>();
```

## Random boolean generation
Provides a way to generate boolean value with the given probability
```csharp
using DotNext;

var rand = new Random();
var b = rand.NextBoolean(0.3D); //0.3 is a probability of TRUE value
```

The same extension method is provided for class [RandomNumberGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator).

# String extensions
Related class: [StringExtensions](xref:DotNext.StringExtensions), [Span](xref:DotNext.Span)

## Reverse string
Extension method _Reverse_ allows to reverse string characters and returns a new string:
```csharp
using DotNext;

var str = "abc".Reverse(); //str is "cba"
```

## Trim by length
Extension method _TrimLength_ limits the length of string or span:
```csharp
using DotNext;

var str = "abc".TrimLength(2);  //str is "ab"
str = "abc".TrimLength(4);  //str is "abc"

Span<int> array = new int[] { 10, 20, 30 };
array = array.TrimLength(2);    //array is { 10, 20 }
```

# Delegates
Related classes: [DelegateHelpers](xref:DotNext.DelegateHelpers).

## Change type of delegate
Different types of delegates can refer to the same method. For instance, `Func<string, string>` represents the same signature as `Converter<string, string>`. It means that the delegate instance can be converted into another delegate type if signature matches.
```csharp
using DotNext;

Func<string, int> lengthOf = str => str.Length;
Converter<string, int> lengthOf2 = lengthOf.ChangeType<Converter<string, int>>();
```

## Predefined delegates
Cached delegate instances for mostly used delegate types: [Predicate&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.predicate-1), [Func&lt;T, TResult&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2) and [Action](https://docs.microsoft.com/en-us/dotnet/api/system.action).

```csharp
using DotNext;

Func<int, int> identity = Func<int>.Identity; //identity delegate which returns input argument without any changes
Predicate<bool> truePredicate = Predicate<bool>.Constant(true); // predicate which always returns true
Predicate<bool> falsePredicate = Predicate<bool>.Constant(false); //predicate which always returns false
Predicate<string> nullCheck = Predicate<string>.IsNull; //predicate checking whether the input argument is null
Predicate<string> notNullCheck = Predicate<string>.IsNotNull; //predicate checking whether the input argument is not null
```

## Logical operators for Predicate
Extension methods implementation logical operators for Predicate delegate instances:

```csharp
using DotNext;

Predicate<string> predicate = str => str.Length == 0;
predicate = !predicate;
predicate = predicate & new Predicate<string>(str => str.Length > 2);
predicate = predicate | new Predicate<string>(str => str.Length % 2 == 0);
predicate = predicate ^ new Predicate<string>(Predicate<string>.IsNull);
```

## Delegate Factories
C# 9 introduces typed function pointers. However, conversion between regular delegates and function pointers is not supported. `DelegateHelpers` offers factory methods allowing creation of delegate instances from function pointers. These factory methods support implicit capturing of the first argument as well:
```csharp
using DotNext;

static int GetHashCode(string s)
{
}

delegate*<string, int> hashCode = &GetHashCode;
Func<string, int> openDelegate = Func<string, int>.FromPointer(hashCode);
Func<int> closedDelegate = Func<int>.FromPointer<string, int>(hashCode, "Hello, world!");
```

## Binding
The delegate instance is a combination of the implicit first argument and the target method. Binding is a way to associate the implicit first argument with the delegate. This concept is used widely in [JavaScript](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Function/bind). .NEXT exposes this functionality through `Bind` extension method or `<<` operator. The binding can be chained.
```csharp
Func<int, int, int> sum = (x, y) => x + y;
Func<int, int> f = sum << 42; // bind 42 to x
f.Invoke(12); // returns 42 + 12

Func<int> f2 = sum << 42 << 12; // bind 42 to x and 12 to y, and produce Func<int>
f.Invoke(); // returns 42 + 12
```

`sum << 42` can be replaced with `sum.Bind(42)` call that has the same effect.

# Range check
Checks whether the given value is in specific range.
```csharp
using DotNext;

var b = 10.IsBetween(5.Enclosed, 11.Enclosed); //b == true
b = 10.IsBetween(0.Disclosed, 4.Disclosed); //b == false
```

# Equality check
Related classes: [BasicExtensions](xref:DotNext.BasicExtensions).

Extension method _IsOneOf_ allows to check whether the value is equal to one of the given values.

```csharp
using DotNext;

var b = 42.IsOneOf(0, 5, 42, 3); //b == true

b = "a".IsOneOf("b", "c", "d"); //b == false
```

## Equality check
_BitwiseEquals_ extension method performs bitwise equality between two regions of memory referenced by the arrays. Element type of these arrays should be of unmanaged value type, e.g. `int`, `long`, `System.Guid`.

```csharp
var array2 = new int[] { 1, 2, 3 };
array2.BitwiseEquals(new [] {1, 2, 4});    //false
```

# Timestamp
[Timestamp](xref:DotNext.Diagnostics.Timestamp) value type can be used as allocation-free alternative to [Stopwatch](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch) when you need to measure time intervals.

```csharp
using DotNext.Diagnostics;
using System;

var timestamp = new Timestamp();
//long-running operation
Console.WriteLine(timestamp.Elapsed);
```

`Elapsed` property returning value of [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan) type which indicates the difference between `timestamp` and the current point in time.

This type should not be used as a unique identifier of some point in time. The created time stamp may identify the time since the start of the process, OS, user session or whatever else.

# Dynamic Task Result
In .NET it is not possible to obtain a result from [task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1) if its result type is not known at compile-time. It can be useful if you are writing proxy or SOAP Middleware using ASP.NET Core and task type is not known for your code. .NEXT provides two ways of doing that:
1. Synchronous method `GetResult` which is located in [Synchronization](xref:DotNext.Threading.Tasks.Synchronization) class
1. Asynchronous method `AsDynamic` which is located in [Conversion](xref:DotNext.Threading.Tasks.Conversion) class.

All you need is to have instance of non-generic [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task) class because all types tasks derive from it.

Under the hood, .NEXT uses Dynamic Language Runtime feature in combination with high-speed optimizations.

The following example demonstrates this approach:
```csharp
using DotNext.Threading.Tasks;
using System.Threading.Tasks;

//assume that t is of unknown Task<T> type
Task t = Task.FromResult("Hello, world!");

//obtain result synchronously
Result<dynamic> result = t.GetResult(CancellationToken.None);

//obtain result asynchronously
dynamic result = await t.AsDynamic();
```

# Ref-like structs in generic context
[LocalReference&lt;T&gt;](xref:DotNext.LocalReference`1) is a simple wrapper by-ref struct that allows to encapsulate the managed pointer to any arbitrary type, including other by-ref structs. This type allows to pass typed managed pointers as generic arguments, as well as holding the managed pointer to by-ref struct within another by-ref struct, which is currently restricted by C# compiler.
```csharp
int i = 42;
LocalReference<int> reference = new(ref i);
```

Under the hood, [LocalReference&lt;T&gt;](xref:DotNext.LocalReference`1) type is the same as `ref T` type. [ReadOnlyLocalReference&lt;T&gt;](xref:DotNext.ReadOnlyLocalReference`1) type is the same as `ref readonly T` type.