Base Class Library Enhancements
====

# Randomization
Related class: [RandomExtensions](../../api/DotNext.RandomExtensions.yml)

Extension methods for random data generation extends both classes _System.Random_ and _System.Security.Cryptography.RandomNumberGenerator_.

## Random string generation
Provides a way to generate random string of the given length and set of allowed characters.
```csharp
using System;
using DotNext;

var rand = new Random();
var password = rand.NextString("abc123", 10);   
//now password has 10 characters
//each character is equal to 'a', 'b', 'c', '1', '2' or '3'
```

The same extension method is provided for class _System.Security.Cryptography.RandomNumberGenerator_.

## Random boolean generation
Provides a way to generate boolean value with the given probability
```csharp
using DotNext;

var rand = new Random();
var b = rand.NextBoolean(0.3D); //0.3 is a probability of TRUE value
```

The same extension method is provided for class _System.Security.Cryptography.RandomNumberGenerator_.

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
Conversion between mostly used delegate types: `Predicate<T>`, `Func<I, O>` and `Converter<I, O>`.

```csharp
using DotNext;

Predicate<string> isEmpty = str => str.Length == 0;
Func<string, bool> isEmptyFunc = isEmpty.AsFunc();
Converter<string, bool> isEmptyConv = isEmpty.AsConverter();
```

## Predefined delegates
Cached delegate instances for mostly used delegate types: `Predicate<T>`, `Func<I, O>` and `Converter<I, O>`.

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
Related classes: [Comparable](../../api/DotNext.Comparable.yml), [Range](../../api/DotNext.Range.yml).

## Min/max value
Generic methods for comparable data types:
```csharp
using DotNext;

var str = "ab".Max("bc"); //str == "bc"
str = "ab".Min("bc");
```

## Restrictions
Restricts the value using upper bound, lower bound or both.
```csharp
using DotNext;

var i = 10.UpperBounded(5); //i == 5
i = 10.UpperBounded(11); //i == 10
i = 10.LowerBounded(11); //i == 11
i = 10.LowerBounded(0); // i == 10
i = 5.Clamp(4, 10); //i == 5
i = 5.Clamp(6, 10); //i == 6
i = 5.Clamp(0, 4); //i == 4
```

## Range check
Checks whether the given value is in specific range.
```csharp
using DotNext;

var b = 10.Between(5, 11, BoundType.Closed); //b == true
b = 10.Between(0, 4); //b == false
```

# Equality check
Related classes: [Comparable](../../api/DotNext.ObjectExtensions.yml), [Range](../../api/DotNext.ValueTypeExtensions.yml).

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
There are two extension methods for equality check of each element between two arrays:
* _SequenceEqual_ performs equality check between each element if element type implements interface `IEquatable<T>`
* _BitwiseEquals_ performs bitwise equality between two regions of memory referenced by the arrays. Element type of these arrays should be of unmanaged value type, e.g. `int`, `long`, `System.Guid`.

```csharp
var array1 = new string[] {"ab", "bc"};
array1.SequenceEqual(new [] {"ab", "bc"}); //true
var array2 = new int[] { 1, 2, 3 };
array2.BitwiseEquals(new [] {1, 2, 4});    //false
```

These methods are fast in comparison to naive implementation using `foreach` iteration and comparison by index. Read [Benchmarks](../../benchmarks.md) for more information.

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