Result Type
====
[Result&lt;T&gt;](xref:DotNext.Result`1) is similar to [std::result](https://doc.rust-lang.org/std/result/) data type in Rust programming language and allows to handle method call errors without `try-catch` block. The value can contain actual result returned from method or error in the form of exception. 

.NET library provides `TryInvoke` extension methods to return `Result<T>` from popular delegate types such as [Func](https://docs.microsoft.com/en-us/dotnet/api/system.func-1), [Converter](https://docs.microsoft.com/en-us/dotnet/api/system.converter-2). The type behaves like monad and the pipeline of calls can be constructed.

```csharp
using DotNext;

Func<string, int> parser = int.Parse;
Result<int> result = parser.TryInvoke("42");
if (result)  //successful
{
    var i = (int)result;
}
```

`Value` property can be used to extract the value of the result, or throws the exception if the result is not available.

```csharp
using DotNext;

Func<string, int> parser = int.Parse;
Result<int> result = parser.TryInvoke("42");
int value = result.Value; // may throw
```

`Error` property can be used to inspect the underlying exception:
```csharp
using DotNext;

Func<string, int> parser = int.Parse;
Result<int> result = parser.TryInvoke("42");
if (result.IsSuccessful)  //successful
{
    var i = result.ValueOrDefault;
}
else
{
    Exception e = result.Error;
}
```

> [!NOTE]
> It's not recommended to rethrow the exception by using `Error` property. It that case, you lose the information about initial stack trace. If you need to capture the state of the exception or rethrow it, use [ExceptionDispatchInfo](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.exceptionservices.exceptiondispatchinfo) class.

Result type is paired with [Optional](xref:DotNext.Optional`1) data type. `Result<T>` can be converted to it implicitly. But conversion loses information about exception:

```csharp
using DotNext;

Func<string, int> parser = int.Parse;
Optional<int> result = parser.TryInvoke("42");
```

# Custom error codes
[Result&lt;T, TError&gt;](xref:DotNext.Result`2) is an overloaded type that allows to use custom error codes instead of exceptions. The second generic parameter expects **enum** type that enumerates all possible error codes.

```csharp
using DotNext;

enum ErrorCode
{
    Success = 0,
    InvalidInputString,
}

static Result<int, ErrorCode> TryParse(string input) => int.TryParse(input) ? input : new(ErrorCode.InvalidInputString);
```

Both types are interoperable:
```csharp
Result<int, ErrorCode> result1 = TryParse("123");
Result<int> result2 = result1;
```