Ranges and Indicies
====
C# 8 introduced [ranges and indicies](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges) for slicing and indexing collections at runtime. These operators are missing in Expression Trees. To bridge the gap, .NEXT Metaprogramming library offers the following expressions:

* [Index Expression](xref:DotNext.Linq.Expressions.ItemIndexExpression) allows to construct expression of type [Index](https://docs.microsoft.com/en-us/dotnet/api/system.index)
* [Range Expression](xref:DotNext.Linq.Expressions.RangeExpression) allows to construct expression of type [Range](https://docs.microsoft.com/en-us/dotnet/api/system.range)
* [Element Access Expression](xref:DotNext.Linq.Expressions.CollectionAccessExpression) allows to construct access to the individual element of the arbitrary collection using [Index](https://docs.microsoft.com/en-us/dotnet/api/system.index). This type of expression is based on _Index Expression_
* [Slice Expression](xref:DotNext.Linq.Expressions.SliceExpression) allows to obtain a slice of the arbitrary collection using [Range](https://docs.microsoft.com/en-us/dotnet/api/system.range). This type of expression is based on _Range Expression_.

_Element Access_ and _Slice_ expressions follow the same rules as described [here](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges) for C# language so library types such as one-dimensional arrays and strings are supported out-of-the-box.

The following example demonstrates how to obtain individual character from string and slice the string:

```csharp
using DotNext.Linq.Expressions;
using System.Linq.Expressions;

Expression stringValue = "Hello, world".Const();
var character = stringValue.ElementAt(2.Index(false));
var slice = stringValue.Slice(start: 0.Index(false), end: 1.Index(true));

//the code is equivalent to
var stringValue = "Hello, world";
var character = stringValue[new Index(2, false)];
var slice = stringValue[0..^1];
```

`Index`, `ElementAt` and `Slice` are extension methods from [ExpressionBuilder](xref:DotNext.Linq.Expressions.ExpressionBuilder) simplifying construction of _Index Expressio_, _Element Access Expression_ and _Slice Expression_ respectively.