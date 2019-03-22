Multi-line Lambdas
====
Expression Trees in C# doesn't support multi-line lambda expressions. This limitation can be avoided using [LambdaBuilder](../../api/DotNext.Metaprogramming.LambdaBuilder-1.yml). The builder represents lexical scope and a lot of methods for lambda body construction.

The following example shows how to generate lambda function which performs factorial computation using recursion:

```csharp
using System;
using DotNext.Metaprogramming;

Func<long, long> fact = LambdaBuilder<Func<long, long>>.Build(fun => 
{
    UniversalExpression arg = fun.Parameters[0];
    fun.If(arg > 1L)
        .Then(arg * fun.Self.Invoke(arg - 1L))
        .Else(arg)
    .OfType<long>()
    .End();
}).Compile();
fact(3);    // == 6
```

`fun` parameter represents lexical scope of the lambda function to be constructed. `arg` local variable is just a conversion of the lambda function parameter into [Universal Expression](universal.md). `fun.If` starts construction of _if-then-else_ expression. `fun.Self` is a reference to the function itself providing recursive access. `OfType` describes type of conditional expression that was started by `fun.If` call. `End` method call represents end of conditional expression. `fun.Parameters` is a collection of lambda parameters sorted by their position in the signature.

The last expression inside of lamda function is interpreted as return value. However, explicit return is supported.

```csharp
using System;
using DotNext.Metaprogramming;

Func<long, bool> isZero = LambdaBuilder<Func<long, bool>>.Build(fun => 
{
    UniversalExpression arg = fun.Parameters[0];
    fun.If(arg != 0L)
        .Then(then => then.Return(true))
        .Else(otherwise => otherwise.Return(false))
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

`then` and `otherwise` parameters provide access to the lexical scope and code block of the positive and negative conditional branches respectively.

# Implicit result
Lambda function builder has implicitly declared _Result_ variable which can be used to set function result without returning from it. If _Result_ variable is not used then builder performs optimization and will not emit declaration of local variable in the final expression tree.

```csharp
LambdaBuilder<Fun<long, long>>(fun => 
{
    fun.Assign(fun.Result, fun.Parameters[0]);
});
```

This feature is similar to _result_ implicit variable in Object Pascal. Once result assigned, it is not needed to use explicit _Return_ method to return from the lambda function.