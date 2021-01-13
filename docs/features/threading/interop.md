Interop with Other Asynchronous Patterns and Types
====
.NEXT Threading library simplifies interop between Task-based programming model and other asynchronous patterns. Bridge methods located in [AsyncBridge](../../api/DotNext.Threading.AsyncBridge.yml) class.

# Cancellation Token
If you have only [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) and you need to synchronize with its cancellation then use `WaitAsync` extension method to do that. It converts token into awaitable object.

```csharp
using DotNext.Threading;
using System.Threading;

CancellationToken token = ...;
await token.WaitAsync();
```

# Wait Handles
[WaitHandle](https://docs.microsoft.com/en-us/dotnet/api/system.threading.waithandle) represents OS-specific object that wait for exclusive access to shared resources. It can be converted into awaitable object using `WaitAsync` static method.

```csharp
using DotNext.Threading;
using System.Threading;

var resetEvent = new ManualResetEvent(false);
await resetEvent.WaitAsync();  
```