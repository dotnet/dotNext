Universal Expression
====
[Universal Expression](../../api/DotNext.Metaprogramming.UniversalExpression.yml) is a powerful tool which allows to use programming language constructs without imitating them using API calls. Compound expressions can be constructed in natural way using operators and constants like in regular code.

```csharp
using DotNext.Metaprogramming;

UniversalExpression i = 10; //int const
UniversalExpression g = 20; //int const
i += g; //now i is expression tree representing binary addition operation
```

Universal expression can be converted into or from [Expression class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression).

```csharp
using System.Linq.Expressions;
using DotNext.Metaprogramming;

UniversalExpression i = 10;
i = i.Convert<long>() * 20L;
Expression expr = i;
```

Properties and methods are accessible using appropriate methods of the universal expression:

```csharp
UniversalExpression str = "Hello, world";
var length = str.Property(nameof(string.Length));   //"Hello, world".Length
```