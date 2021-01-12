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
    using (var s = new MemoryStream(bytes))
    {
        s.Flush();
    }
});
```

`stream` parameter represents disposable resource inside of `Using()` scope.

_Using_ statement can accept any expression of type implementing `System.IDisposable` interface or having public instance method `Dispose()` without parameters.

## Async Disposable
**await using** statement in C# allows to control the lifetime of the resource implementing [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface. The same statement is supported by Metaprogramming library:
```csharp
using System.IO;
using System.Threading.Tasks;
using static DotNext.Metaprogramming.CodeGenerator;

AsyncLambda<Func<byte[], Task>>(fun => 
{
    AwaitUsing(typeof(MemoryStream).New(fun[0]), stream => 
    {
        Call(stream, nameof(MemoryStream.Flush));
    });
});

//the generated code is
new Func<byte[], Task>(async bytes => 
{
  await using (var s = new MemoryStream(bytes))
  {
      s.Flush();
  }
});
```

This type of statement is allowed within async lambda expression only.