Multi-line Lambdas
====
Expression Trees in C# doesn't support multi-line lambda expressions. This limitation can be avoided using [CodeGenerator.Lambda](../../api/DotNext.Metaprogramming.CodeGenerator.yml) construction method. The method accepts lexical scope of the lambda expression in the form of the delegate and provides access to parameters.

The following example shows how to generate lambda function which performs factorial computation using recursion:

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Func<long, long> fact = Lambda<Func<long, long>>(fun => 
{
    var arg = (U)fun[0];
    If(arg > 1L)
        .Then(arg * fun.Invoke(arg - 1L))
        .Else(arg)
		.OfType<long>()
    .End();
}).Compile();
fact(3);    // == 6
```

`fun` parameter is of type [LambdaContext](../../api/DotNext.Metaprogramming.LambdaContext.yml) and provide access to the function parameters. `arg` local variable is just a conversion of the lambda function parameter into [Universal Expression](universal.md). `If` starts construction of _if-then-else_ expression. `fun.Invoke` method allows to invoke lambda function recursively. `OfType` describes type of conditional expression that was started by `If` call. `End` method call represents end of conditional expression.

`LambdaContext` type supports decomposition so it is possible to use convenient syntax to obtain function parameters:
```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Lambda<Func<int, int, int>>(fun => 
{
  var (x, y) = fun;
  Return((U)x + y);
});
```

The last expression inside of lamda function is interpreted as return value. However, explicit return is supported.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Func<long, bool> isZero = Lambda<Func<long, bool>>(fun => 
{
    var arg = (U)fun[0];
    If(arg != 0L)
        .Then(() => Return(true))
        .Else(() => Return(false))
    .End();
}).Compile();
```

The equivalent code is
```csharp
using System;

new Func<long, long>(arg => 
{
    if(arg != 0L)
        return true;
    else
        return false;
});
```

# Implicit result
Lambda function builder can have optional _result_ variable which is used to set function result without returning from it.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Fun<long, long>>((fun, result) => 
{
    Assign(result, fun[0]);
});
```

This feature is similar to _result_ implicit variable in [Pascal](https://www.freepascal.org/docs-html/ref/refse90.html) programming language. Once result assigned, it is not needed to use explicit _Return_ method to return from the lambda function.