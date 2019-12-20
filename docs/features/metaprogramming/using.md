Using Statement
====
[using](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement) statement is a missing part of LINQ Expressions. Metaprogramming library provides such support. 

```csharp
using DotNext.Linq.Expressions;
using System;
using System.IO;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Action<byte[]>>(fun => 
{
    Using(typeof(MemoryStream).New(fun[0]), stream => 
    {
        Call(stream, nameof(MemoryStream.Flush));
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

`stream` parameter represents disposable resource inside of `Using()` scope.

_Using_ statement can accept any expression of type implementing `System.IDisposable` interface or having public instance method `Dispose()` without parameters.