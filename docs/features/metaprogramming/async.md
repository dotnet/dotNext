Async Lambda
====
Metaprogramming library provides full support of dynamic generation of [async lambda functions](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async). This functionality is not supported by LINQ Expressions out-of-the-box.

There are three key elements required to generated async lambda:
* [AsyncLambdaBuilder](../../api/DotNext.Metaprogramming.AsyncLambdaBuilder-1.yml) used to build async lambda function instead of [LambdaBuilder](../../api/DotNext.Metaprogramming.LambdaBuilder-1.yml) suitable for synchronous lambda functions only.
* [AsyncResultExpression](../../api/DotNext.Metaprogramming.AsyncResultExpression.yml) used to return value from the asynchronous lambda function (known as async return). Usually, the developer don't need to use this type of expression explicitly.
* [AwaitExpression](../../api/DotNext.Metaprogramming.AwaitExpression.yml) is similar to [await](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await) operator.

**await** operator can be used even in _try-catch-finally_ statement and means that async lambda function works in the same way as **async** methods in C#.

Let's translate the following example of async method in C# programming language. 
```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

private static async Task<long> GetPageSizeAsync(string url)  
{  
    var client = new HttpClient();  
    var uri = new Uri(url);
    var urlContents = await client.GetByteArrayAsync(uri);
    return urlContents.LongLength;
}  
```

`Await()` extension method applicable to any object of type [Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) is an equivalent of **await** operator and do all necessary magic. Note that `Await()` is applicable inside of async lambda function. If **await** operator is used inside of synchronous async lambda function then compiled code will block the thread during execution of the expression used as an argument for this operator.

```csharp
using DotNext.Metaprogramming;
using System;
using System.Net.Http;
using System.Threading.Tasks;

AsyncLambdaBuilder<Func<string, Task<long>>>(fun => 
{
    var client = fun.DeclareVariable("client", typeof(HttpClient).New());       //var client = new HttpClient();
    var uri = fun.DeclareVariable("uri", typeof(Uri).New(fun.Parameters[0]));   //var uri = new Uri(url);
    var urlContents = fun.DeclareVariable<byte[]>("urlContents");
    fun.Assign(urlContents, client.Call(nameof(HttpClient.GetByteArrayAsync), uri).Await());    //urlContents = await client.GetByteArrayAsync(uri);
    fun.Assign(fun.Result, urlContents);
});
```

**await** operator can be placed inside of any statement: _switch_, _if-then-else_, _try-catch-finally_ etc.

# Limitations
Async lambda function has the following limitations:
* The return type of the delegate representing lambda function should have one of the following return types:
    * [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task)
    * [Task&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1)
* All limitations inherited from LINQ Expression framework which are described [here](index.md)

**void** return type is not supported as well as [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask). Instead of **void** return type use _Task_ class.

> [!TIP]
> Support of arbitrary async return type (such as ValueTask) may be a subject to future improvements. To do that, Metaprogramming code generator should recognize the type marked with [AsyncMethodBuilderAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncmethodbuilderattribute) and extract the information needed for code generation.

However, **await** operator is applicable to any async method with return type which differs from `Task` or `Task<R>` types without limitations.

# Code Generation Principles
Metaprogramming library transforms the body of async lambda function into state machine. The algorithm of transformation is similar to [Roslyn](https://github.com/dotnet/roslyn) Compiler but not equal. You can start from [this](https://github.com/dotnet/roslyn/blob/master/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs) source code to learn how Roslyn transforms the code of async method.

The data type used to store local variables in the form of machine state is not compiler-generated because LINQ Expression doesn't have dynamic type generation functionality. Value tuples are used instead of dynamically generated type.

_try-catch-finally_ block is transformed into code without structured exception handling instructions. The exception management is centralized by [async state machine](../../api/DotNext.Runtime.CompilerServices.AsyncStateMachine-1.yml).

The exception raised by the async lambda function is stored inside of async state machine, not as separated field in the machine state value type generated by Roslyn Compiler.

[Async State Machine](../../api/DotNext.Runtime.CompilerServices.AsyncStateMachine-1.yml) class contains low-level methods allow to control execution of async lambda function. To be more precise, there are two async state machine classes:
* [AsyncStateMachine&lt;TState&gt;](../../api/DotNext.Runtime.CompilerServices.AsyncStateMachine-1.yml) is used by async lambda function without return value (actual return type is [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task))
* [AsyncStateMachine&lt;TState, R&gt;](../../api/DotNext.Runtime.CompilerServices.AsyncStateMachine-1.yml) is used by async lambda function with return value (actual return type is [Task&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1))

> [!CAUTION]
> _AsyncStateMachine_ class is subject to change in future versions of the library. 

The developer can use async state machine directly to write asynchronous methods in .NET languages where **async**/**await** feature is not supported syntactically.