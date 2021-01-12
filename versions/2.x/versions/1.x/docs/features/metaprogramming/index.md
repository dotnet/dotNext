Metaprogramming
====
Metaprogramming API provided by .NEXT library allows to generate and execute code in runtime. Code generation object model is language agnostic so developer can use it from any .NET programming language. From design point of view, metaprogramming capabilities built on top of [LINQ Expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions) without direct usage of IL generator. This increases portability of the library between different .NET implementations. All custom expressions introduced by Metaprogramming libary are reducible into predefined set of LINQ Expressions.

> [!WARNING]
> Xamarin.iOS supports only interpretation of Expression Trees without Just-in-Time Compilation. Since the iPhone's kernel prevents an application from generating code dynamically Mono on the iPhone does not support any form of dynamic code generation. Check out [this article](https://docs.microsoft.com/en-us/xamarin/ios/internals/limitations) for more information. As a result, the code generated using .NEXT Metaprogramming library demonstrates significantly slower performance on iOS.

Metaprogramming library extends LINQ Expression with the following features:
* [String Interpolation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated)
* [using statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement)
* [lock statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement)
* Loops
    * [foreach loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in)
    * [while loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while)
    * [for loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for)
* [With..End statement](https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement)
* Full support of custom [async](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async) lambda functions and [await](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await) expressions
* Extension methods for easy construction of compound expressions and statements
* Universal expression which allows to use standard operators provided by programming language

All these extensions are compatible with [Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) class.

Additionally, .NEXT Metaprogramming library replaces limit of [C# Expression Trees](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) where only single-line lambda expression is allowed.

> [!IMPORTANT]
> In spite of rich set of Metaprogramming API, a few limits still exist. These restrictions dictated by internal design of LINQ Expression. The first, overloaded operators with [in parameter modifier](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/in-parameter-modifier) cannot be resolved. The second, [ref return](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#reference-return-values) and [ref locals](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#ref-locals) are not supported.

# Concept
The code construction based on the following concepts:
* [Universal Expression](universal.md)
* [Code Generator](../../api/DotNext.Metaprogramming.CodeGenerator.yml) provides methods for adding statement such as method calls, assignment, loops, if-then-else statement etc.
* [Expression Builder](../../api/DotNext.Linq.Expressions.ExpressionBuilder.yml) provides extension methods for constructing expressions

[Code expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions) from .NET library heavily extended with additional expression types:
    * [using Expression](../../api/DotNext.Linq.Expressions.UsingExpression.yml) represents `using` statement from C#
    * [lock Expression](../../api/DotNext.Linq.Expressions.LockExpression.yml) represents `lock` statement from C#
    * [await Expression](../../api/DotNext.Linq.Expressions.AwaitExpression.yml) represents `await` operator from C#
    * [String Interpolation Expression](../../api/DotNext.Linq.Expressions.InterpolationExpression.yml) represents interpolated string
    * [for-in Expression](../../api/DotNext.Linq.Expressions.ForEachExpression.yml) represents `foreach` loop from C#
    * [while Expression](../../api/DotNext.Linq.Expressions.WhileExpression.yml) represents `while` and `do-while` loops from C#
    * [for Expression](../../api/DotNext.Linq.Expressions.ForExpression.yml) represents `for` loop from C#

The lexical scope is enclosed by multi-line lambda function. The body of such function contains the code for generation of expressions and statements.

```csharp
using DotNext.Linq.Expressions;
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expression.UniversalExpression;

Func<long, long> fact = Lambda<Func<long, long>>(fun => 
{
    var arg = (U)fun[0];    //convert parameter into universal expression
    //if-then-else expression
    If(arg > 1L)
        .Then(arg * fun.Invoke(arg - 1L))  //recursive invocation of the current lambda function
        .Else(arg)  //else branch
        .OfType<long>()
    .End();
    //declare local variable of type long
    var local = DeclareVariable<long>("local");
    //assignment
    Assign(local, -arg);  //equivalent is the assignment statement local = -arg
    //try-catch
    Try(() => 
    {
        Return(10.Const());    //return from lambda function
    })
    .Finally(() => //finally block
    {  
        //method call Console.WriteLine(local);
        CallStatic(typeof(Console), nameof(Console.WriteLine), local);
    })
    .End(); //end of try block
}).Compile();
```

Statement construction methods from `CodeGenerator` mimic syntax of C# programming language that improves maintainability of the code.