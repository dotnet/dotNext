Compound Expressions Construction
====

[Expression class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) provides builder static methods for constructing expression trees. The construction code loss its readability because builder methods oriented for tree-like representation of the final expression. Metaprogramming library offers set of [extension methods](xref:DotNext.Linq.Expressions.ExpressionBuilder) aimed to simplification of expression tree construction.

```csharp
using DotNext.Metaprogramming;

var expr = (-(42.Quoted).Convert<long>()) + 1L.Quoted; // equivalent to -((long)42) + 1L
```

The following example demonstrates how to construct `throw` statement:

```csharp
using System;
using DotNext.Metaprogramming;

typeof(Exception).New("Exception message".Quoted).Throw()    //equivalent to new Exception("Exception message")
```

Compound expression can be constructed with **dynamic** type:

```csharp
using System;
using DotNext.Metaprogramming;

dynamic expr = 42.Quoted.AsDynamic();
expr = -expr + 1;
Expression tree = expr; //tree is -42 + 1
```

or with overloaded operators:
```csharp
using System;
using DotNext.Metaprogramming;

Expression expr = 42.Quoted;
expr = -expr + 1.Quoted; //expr is -42 + 1
```

Checked and unchecked versions of the arithmetic operators are supported.