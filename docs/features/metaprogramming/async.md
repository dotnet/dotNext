Async Lambda
====
Metaprogramming library provides full support of dynamic generation of [async lambda functions](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async). This functionality is not supported by LINQ Expressions out-of-the-box.

There are three key elements required to generated async lambda:
* [AsyncLambdaBuilder](../../api/DotNext.Metaprogramming.AsyncLambdaBuilder-1.yml) used to build async lambda function
* [AsyncResultExpression](../../api/DotNext.Metaprogramming.AsyncResultExpression.yml) used to return value from the asynchronous lambda function
* [AwaitExpression](../../api/DotNext.Metaprogramming.AwaitExpression.yml) is similar to [await](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await) operator.