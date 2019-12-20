Compound Expressions Construction
====

[Expression class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) provides builder static methods for constructing expression trees. The construction code loss its readability because builder methods oriented for tree-like representation of the final expression. Metaprogramming library offers set of [extension methods](../../api/DotNext.Linq.Expressions.ExpressionBuilder.yml) aimed to simplification of expression tree construction.

```csharp
using DotNext.Metaprogramming;

var expr = 42.Const().Convert<long>().Negate().Add(1L.Const()); // equivalent to -((long)42) + 1L
```

The following example demonstrates how to construct `throw` statement:

```csharp
using System;
using DotNext.Metaprogramming;

typeof(Exception).New("Exception message".Const()).Throw()    //equivalent to new Exception("Exception message")
```

Compound expression can be constructed with **dynamic** type:

```csharp
using System;
using DotNext.Metaprogramming;

dynamic expr = (UniversalExpression)42;
expr = -expr + 1;
Expression tree = expr; //tree is -42 + 1
```