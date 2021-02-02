Asynchronous Delegates
====
C# compiler adds `BeginInvoke` and `EndInvoke` methods for each custom delegate type. However, these methods throw [NotSupportedException](https://docs.microsoft.com/en-us/dotnet/api/system.notsupportedexception) in .NET Core because they depend on .NET Remoting. You can read more about this limitation [here](https://github.com/dotnet/corefx/issues/5940).

.NEXT library provides alternative to these methods for asynchronous invocation of synchronous delegates. Extension method `InvokeAsync` which is located in [AsyncDelegate](xref:DotNext.Threading.AsyncDelegate) class overloaded for commonly used delegate types from .NET library. The implementation executes every single method in the invocation list asynchronously because all delegates in C# are multicast delegates.

```csharp
using DotNext.Threading;
using System;

Action action = Method1;
action += Method2;
action += Method3;

await action.InvokeAsync();
```