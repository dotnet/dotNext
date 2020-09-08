Building Expression Trees
====
The expression tree can be constructed using [dynamic](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/types/using-type-dynamic) type provided by C# programming language or similar feature in other .NET languages. As a result, expression tree looks like a true language expression or statement without method calls such as `Property()` or `Field()` for member access.

This feature converts any dynamic expression into expression tree. The starting point is [ExpressionBuilder.AsDynamic](../../api/DotNext.Linq.Expressions.ExpressionBuilder.yml) extension method. After all necessary manipulations with expression tree it is possible to convert dynamic type to [Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) type back again.

```csharp
using DotNext.Linq.Expressions;
using System.Linq.Expressions;

dynamic expr = "Hello, world".Const().AsDynamic();
expr = expr.Length;
Expression e = expr;    //equivalent is "Hello, world".Length
expr = 10L.Const().AsDynamic();
expr = expr + 42L;      //equivalent is 10L + 42L
```

Unfortunately, due to limitations of C# programming language, construction of **new** expression or type cast is not available. Delegate invocation expression, method call expression, indexer properties and fields are fully supported.