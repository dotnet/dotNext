Using Statement
====
[using](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement) statement is a missing part of LINQ Expressions. Metaprogramming library provides such support. 

```csharp
using System;
using System.IO;
using DotNext.Metaprogramming;

LambdaBuilder<Action<byte[]>>.Build(fun => 
{
    fun.Using(typeof(MemoryStream).New(fun.Parameters[0]), usingBlock => {
        usingBlock.Call(usingBlock.DisposableVar, nameof(MemoryStream.Flush));
    });
});

//the generated code is
new Action<byte>(bytes =>
{
    using(var s = new MemoryStream(bytes))
    {
        s.Flush();
    }
});
```

Disposable resource is accessible using _DisposableVar_ property of the statement builder. 

_Using_ statement can accept any expression of type implementing `System.IDisposable` interface or having public instance method `Dispose()` without parameters.