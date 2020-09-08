Universal Expression
====
[Universal Expression](../../api/DotNext.Linq.Expressions.UniversalExpression.yml) is a powerful tool which allows to use programming language constructs without imitating them using API calls. Compound expressions can be constructed in natural way using operators and constants like in regular code.

```csharp
using DotNext.Linq.Expressions;

UniversalExpression i = 10; //int const
UniversalExpression g = 20; //int const
i += g; //now i is expression tree representing binary addition operation
```

Universal expression can be converted into or from [Expression class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression).

```csharp
using DotNext.Linq.Expressions;
using System.Linq.Expressions;

UniversalExpression i = 10;
i = i.Convert<long>() * 20L;
Expression expr = i;
```

Properties and methods are accessible using appropriate methods of the universal expression:

```csharp
using DotNext.Linq.Expressions;

UniversalExpression str = "Hello, world";
var length = str.Property(nameof(string.Length));   //"Hello, world".Length
```

# DLR Integration
The expression tree can be constructed using [dynamic](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/types/using-type-dynamic) type provided by C# programming language or similar feature in other .NET languages. As a result, expression tree looks like a true language expression or statement without method calls such as `Property()` or `Field()` for member access.

This feature converts any dynamic expression into expression tree. The starting point is an instance of _UniversalExpression_ which can be converted into **dynamic** data type. After all necessary manipulations with expression tree it is possible to convert dynamic type into _Expression_ or _UniversalExpression_ type back again.

```csharp
using DotNext.Linq.Expressions;
using System.Linq.Expressions;

dynamic expr = (UniversalExpression)"Hello, world";
expr = expr.Length;
Expression e = expr;    //equivalent is "Hello, world".Length
expr = (UniversalExpression)10L;
expr = expr + 42L;
e = expr; //equivalent is 10L + 42L
```

Unfortunately, due to limitations of C# programming language, construction of **new** expression or type cast is not available. Delegate invocation expression, method call expression, indexer properties and fields are fully supported.