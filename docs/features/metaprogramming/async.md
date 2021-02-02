Async Lambda
====
Metaprogramming library provides full support of dynamic generation of [async lambda functions](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async). This functionality is not supported by LINQ Expressions out-of-the-box.

There are three key elements required to generated async lambda:
* [CodeGenerator.AsyncLambda](xref:DotNext.Metaprogramming.CodeGenerator) method used to build async lambda function instead of `CodeGenerator.Lambda` method suitable for synchronous lambda functions only.
* [AsyncResultExpression](xref:DotNext.Linq.Expressions.AsyncResultExpression) used to return value from the asynchronous lambda function (known as async return). Usually, the developer don't need to use this type of expression directly.
* [AwaitExpression](xref:DotNext.Linq.Expressions.AwaitExpression) is similar to [await](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await) operator.

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

`Await()` extension method from [ExpressionBuilder](xref:DotNext.Linq.Expressions.ExpressionBuilder) applicable to any object of type [Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) is an equivalent of **await** operator and do all necessary magic. Note that `Await()` is applicable inside of async lambda function. If **await** operator is used inside of synchronous async lambda function then compiled code will block the thread during execution of the expression used as an argument for this operator.

```csharp
using DotNext.Linq.Expressions;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using static DotNext.Metaprogramming.CodeGenerator;

AsyncLambda<Func<string, Task<long>>>((fun, result) => 
{
    var client = DeclareVariable("client", typeof(HttpClient).New());       //var client = new HttpClient();
    var uri = DeclareVariable("uri", typeof(Uri).New(fun[0]));   //var uri = new Uri(url);
    var urlContents = DeclareVariable<byte[]>("urlContents");
    Assign(urlContents, client.Call(nameof(HttpClient.GetByteArrayAsync), uri).Await());    //urlContents = await client.GetByteArrayAsync(uri);
    Assign(result, urlContents);
});
```

**await** operator can be placed inside of any statement: _switch_, _if-then-else_, _try-catch-finally_ etc.

`AsyncLambda` factory method is overloaded by two versions of this method:
* `AsyncLambda((fun, result) => { })` introduces special variable `result` that can be used to assign result of the function. This approach is similar to [Result](https://www.freepascal.org/docs-html/ref/refse90.html) variable in Pascal programming language.
* `AsyncLambda(fun => { })` doesn't introduce special variable for the function result and control transfer to the caller is provided by `CodeGenerator.Return` method.

`fun` parameter is of type [LambdaContext](xref:DotNext.Metaprogramming.LambdaContext) that provides access to the function parameters.

# Limitations
Async lambda function has the following limitations:
* The return type of the delegate representing lambda function should have one of the following return types:
    * [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task)
    * [Task&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1)
    * [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask)
    * [ValueTask&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1)
* All limitations inherited from LINQ Expression framework which are described [here](index.md)

**void** return type is not supported. Instead of **void** return type use _Task_ or _ValueTask_.

However, **await** operator is applicable to any async method with return type which differs from supported task types without limitations.

# Code Generation Principles
Metaprogramming library transforms the body of async lambda function into state machine. The algorithm of transformation is similar to [Roslyn](https://github.com/dotnet/roslyn) Compiler but not equal. You can start from [this](https://github.com/dotnet/roslyn/blob/master/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/AsyncMethodToStateMachineRewriter.cs) source code to learn how Roslyn transforms the code of async method.

The data type used to store local variables in the form of machine state is not compiler-generated because LINQ Expression doesn't have dynamic type generation functionality. Value tuples are used instead of dynamically generated type.

_try-catch-finally_ block is transformed into code without structured exception handling instructions. The exception management is centralized by [async state machine](https://github.com/sakno/DotNext/blob/master/src/DotNext.Metaprogramming/Runtime/CompilerServices/AsyncStateMachine.cs).

The exception raised by the async lambda function is stored inside of async state machine, not as separated field in the machine state value type generated by Roslyn Compiler.

Async State Machine class contains low-level methods allow to control execution of async lambda function. To be more precise, there are two async state machine classes:
* _AsyncStateMachine&lt;TState&gt;_ is used by async lambda function without return value.
* _AsyncStateMachine&lt;TState, R&gt;_ is used by async lambda function with return value.

Implementation of the state machine optimized for situations when asynchronous method completed synchronously. In this case, the state machine will not be boxed.

> [!CAUTION]
> _AsyncStateMachine_ class is internal type and is not available publicly.