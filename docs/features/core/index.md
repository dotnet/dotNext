Base Class Library Enhancements
====

# Randomization
Related class: [RandomExtensions](../../api/DotNext.RandomExtensions.yml)

Extension methods for random data generation extends both classes [Random](https://docs.microsoft.com/en-us/dotnet/api/system.random) and [RandomNumberGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator).

## Random string generation
Provides a way to generate random string of the given length and set of allowed characters.
```csharp
using DotNext;
using System;

var rand = new Random();
var password = rand.NextString("abc123", 10);   
//now password has 10 characters
//each character is equal to 'a', 'b', 'c', '1', '2' or '3'
```

The same extension method is provided for class [RandomNumberGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator).

## Random boolean generation
Provides a way to generate boolean value with the given probability
```csharp
using DotNext;

var rand = new Random();
var b = rand.NextBoolean(0.3D); //0.3 is a probability of TRUE value
```

The same extension method is provided for class [RandomNumberGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator).

# String extensions
Related class: [StringExtensions](../../api/DotNext.StringExtensions.yml).

## Reverse string
Extension method _Reverse_ allows to reverse string characters and returns a new string:
```csharp
using DotNext;

var str = "abc".Reverse(); //str is "cba"
```

## Trim by length
Extension method _TrimLength_ limits string length:
```csharp
using DotNext;

var str = "abc".TrimLength(2);  //str is "ab"
str = "abc".TrimLength(4);  //str is "abc"
```

## Raw Data
Extension method _GetRawData_ allows to obtain managed pointer to the underlying char buffer referenced by the string instance. This method can be used in performance-critical paths in your code
```csharp
using DotNext;

ref readonly char ch = ref "str".GetRawData();  //now ch points to the first string character of 's'
```

Returned managed pointer is immutable because instantiated string cannot be modified at runtime.

# Delegates
Related classes: [DelegateHelpers](../../api/DotNext.StringExtensions.yml), [Func](../../api/DotNext.Func.yml), [Converter](../../api/DotNext.Converter.yml), [Predicate](../../api/DotNext.Predicate.yml).

## Change type of delegate
Different types of delegates can refer to the same method. For instance, `Func<string, string>` represents the same signature as `Converter<string, string>`. It means that the delegate instance can be converted into another delegate type if signature matches.
```csharp
using DotNext;

Func<string, int> lengthOf = str => str.Length;
Converter<string, int> lengthOf2 = lengthOf.ChangeType<Converter<string, int>>();
```

## Create delegate instance
Statically-typed version of delegate creation method shipped with .NET Standard.
```csharp
using DotNext;

var parseInt = typeof(int).GetMethod(nameof(int.Parse)).CreateDelegate<Func<string, int>>();

```

## Specialized delegate converters
Conversion between mostly used delegate types: [Predicate&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.predicate-1), [Func&lt;T, TResult&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2?view=netcore-3.0) and [Converter&lt;TInput, TOutput&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.converter-2).

```csharp
using DotNext;

Predicate<string> isEmpty = str => str.Length == 0;
Func<string, bool> isEmptyFunc = isEmpty.AsFunc();
Converter<string, bool> isEmptyConv = isEmpty.AsConverter();
```

## Predefined delegates
Cached delegate instances for mostly used delegate types: [Predicate&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.predicate-1), [Func&lt;T, TResult&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2?view=netcore-3.0) and [Converter&lt;TInput, TOutput&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.converter-2).

```csharp
using DotNext;

Func<int, int> identity = Func.Identity<int>(); //identity delegate which returns input argument without any changes
Predicate<string> truePredicate = Predicate.True<string>(); // predicate which always returns true
Predicate<object> falsePredicate = Predicate.False<string>(); //predicate which always returns false
Predicate<string> nullCheck = Predicate.IsNull<string>(); //predicate checking whether the input argument is null
Predicate<string> notNullCheck = Predicate.IsNotNull<string>(); //predicate checking whether the input argument is not null
```

## Logical operators for Predicate
Extension methods implementation logical operators for Predicate delegate instances:

```csharp
using DotNext;

Predicate<string> predicate = str => str.Length == 0;
predicate = predicate.Negate();
predicate = predicate.And(str => str.Length > 2);
predicate = predicate.Or(str => str.Length % 2 == 0);
predicate = predicate.Xor(Predicate.IsNull<string>());
```

# Comparable data types
Related class: [Comparable](../../api/DotNext.Comparable.yml)

## Min/max value
Generic methods for comparable data types:
```csharp
using DotNext;

var str = "ab".Max("bc"); //str == "bc"
str = "ab".Min("bc");
```

## Range check
Checks whether the given value is in specific range.
```csharp
using DotNext;

var b = 10.Between(5, 11, BoundType.Closed); //b == true
b = 10.Between(0, 4); //b == false
var i = 5.Clamp(4, 10); //i == 5
i = 5.Clamp(6, 10); //i == 6
i = 5.Clamp(0, 4); //i == 4
```

# Equality check
Related classes: [ObjectExtensions](../../api/DotNext.ObjectExtensions.yml), [ValueTypeExtensions](../../api/DotNext.ValueTypeExtensions.yml).

Extension method _IsOneOf_ allows to check whether the value is equal to one of the given values.

```csharp
using DotNext;

var b = 42.IsOneOf(0, 5, 42, 3); //b == true

b = "a".IsOneOf("b", "c", "d"); //b == false
```

# Array extensions
Related classes: [OneDimensionalArray](../../api/DotNext.OneDimensionalArray.yml).

Extension methods for slicing, iteration, conversion, element insertion and fast equality check between elements of two arrays.

## Equality check
_BitwiseEquals_ extension method performs bitwise equality between two regions of memory referenced by the arrays. Element type of these arrays should be of unmanaged value type, e.g. `int`, `long`, `System.Guid`.

```csharp
var array2 = new int[] { 1, 2, 3 };
array2.BitwiseEquals(new [] {1, 2, 4});    //false
```

This method is faster than naive implementation using `foreach` iteration and comparison by index. Read [Benchmarks](../../benchmarks.md) for more information.

## Functional iteration
Extension method `ForEach` allows to iterate over array elements and, optionally, modify array element.

```csharp
var array = new string[] {"ab", "bc" };
array.ForEach((long index, ref string element) => {
    if(element == "ab")
        element = "";
});
```

## Insertion and Removal
Extension methods _Insert_, _Slice_, _RemoveLast_ and _RemoveFirst_ allow to return modified array according with semantics of chosen method:
```csharp
var array = new string[] {"a", "b"};
array = array.Insert("c", 2);   //array == new []{"a", "b", "c"}

array = array.RemoveLast(2);    //array == new []{"a"}

array = new string[] {"a", "b", "c"};
array = array.RemoveFirst(2);   //array == new []{"c"}

array = new string[]{"a", "b", "c", "d"}; 
array = array.Slice(1, 2);      //array == new []{"b", "c"}
```

# Extensions for `IntPtr` and `UIntPtr`
Natural-sized integer data types [IntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr) and [UIntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr) have no arithmetic, bitwise, and comparison operators as other numeric types in .NET standard library. This is fixed by .NEXT library which provides implementation of these operators in the form of extension methods available for both types from [ValueTypeExtensions](../../api/DotNext.ValueTypeExtensions.yml) class.

These methods are implemented as intrinsics using inline IL code so they can be replaced by equivalent assembly instruction by JIT compiler. As a result, they the methods have the same performance as natively supported operators for regular numeric types.

The following example demonstrates how to use these methods:
```csharp
using DotNext;

var i = new IntPtr(40);
i = i.Add(new IntPtr(2));	//i == 42
if(i.GreaterThan(IntPtr.Zero))
	i = i.Subtract(new IntPtr(10));
else
	i = i.OnesComplement();	//equivalent to operator ~
```

# Timestamp
[Timestamp](https://sakno.github.io/dotNext/api/DotNext.Diagnostics.Timestamp.html) value type can be used as allocation-free alternative to [Stopwatch](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch) when you need to measure time intervals.

```csharp
using DotNext.Diagnostics;
using System;

var timestamp = Timestamp.Current;
//long-running operation
Console.WriteLine(current.Elapsed);
```

`Elapsed` property returning value of [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan) type which indicates the difference between `timestamp` and the current point in time.

This type should not be used as unique identifier of some point in time. The created time stamp may identify the time since the start of the process, OS, user session or whatever else.