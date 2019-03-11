Metaprogramming
====
Metaprogramming API provided by DotNext library allows to generate and execute code in runtime. Code generation object model is language agnostic so developer can use it from any .NET programming language. From design point of view, metaprogramming capabilities built on top of [LINQ Expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions) without direct usage of IL generator. This increases portability of the library between different .NET implementations.

> [!WARNING]
> Xamarin.iOS supports only interpretation of Expression Trees without Just-in-Time Compilation. Since the iPhone's kernel prevents an application from generating code dynamically Mono on the iPhone does not support any form of dynamic code generation. Check out [this article](https://docs.microsoft.com/en-us/xamarin/ios/internals/limitations) for more information. As a result, the code generated using DotNext Metaprogramming library demonstrates significantly slower performance on iOS.

Metaprogramming library extends LINQ Expression with the following features:
* [using statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement)
* Loops
    * [foreach loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in)
    * [while loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while)
    * [for loop](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for)
* [With..End statement](https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement)
* Full support of custom [async](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async) lambda functions and [await](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await) expressions
* Extension methods for easy construction of compound expressions and statements
* Universal expression which allows to use standard operators provided by programming language

All these extensions are compatible with [Expression class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression).

Additionally, DotNext Metaprogramming library replaces limit of [C# Expression Trees](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) where only single-line lambda expression is allowed.

> [!IMPORTANT]
> In spite of rich set of Metaprogramming API, a few limits still exist. These restrictions dictated by internal design of LINQ Expression. The first, overloaded operators with [in parameter modifier](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/in-parameter-modifier) cannot be resolved. The second, [ref return](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#reference-return-values) and [ref locals](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#ref-locals) are not supported.