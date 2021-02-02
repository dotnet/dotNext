Result Type
====
[Result&lt;R&gt;](xref:DotNext.Result`1) is similar to [std::result](https://doc.rust-lang.org/std/result/) data type in Rust programming language and allows to handle method call errors without `try-catch` block. The value can contain actual result returned from method or error in the form of exception. 

.NET library provides `TryInvoke` extension methods to return `Result<T>` from popular delegate types such as [Func](https://docs.microsoft.com/en-us/dotnet/api/system.func-1), [Converter](https://docs.microsoft.com/en-us/dotnet/api/system.converter-2). The type behaves like monad and the pipeline of calls can be constructed.

```csharp
using DotNext;

Func<string, int> parser = int.Parse;
Result<int> result = parser.TryInvoke("42");
if(result)  //successful
{
    var i = (int) result;
}
else
{
    throw result.Error;
}
```

This type is paired with [Optional](xref:DotNext.Optional`1) data type. `Result<T>` can be converted to it implicitly. But conversion loses information about exception:

```csharp
using DotNext;

Func<string, int> parser = int.Parse;
int result = parser.TryInvoke("42").OrInvoke(error => 0);
```
