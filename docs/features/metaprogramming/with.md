with Expression
====
[with Operator](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression) provides nondestructive mutation of the record using copy-on-write approach. .NEXT Metaprogramming provides [special expression type](DotNext.Linq.Expressions.MutationExpression) to support the operator:
```csharp
using DotNext.Linq.Expressions;
using System.Linq.Expressions;

record class MyRecord(int X);

Expression x = new MyRecord(42).Const();
Expression mutation = x.With(new MemberBindings()
{
    {"X", 52.Const()}
});
```
`With` extension method generates the expression equivalent to the following C# expression:
```csharp
MyRecord mutation = x with { X = 52 };
```

Mutation expression supports record classes and record value types.