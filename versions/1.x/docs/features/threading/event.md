Async Reset Event
====
[AsyncManualResetEvent](../../api/DotNext.Threading.AsyncManualResetEvent.yml) and [AsyncAutoResetEvent](../../api/DotNext.Threading.AsyncAutoResetEvent.yml) are asynchronous alternatives to [ManualResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent) and [AutoResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent) with the same semantics. However, it doesn't support interoperation with .NET thread pool and native code because there is no wait handle associated with an instance of event.

```csharp
using DotNext.Threading;
using System;
using System.Threading;

var resetEvent = new AsyncManualResetEvent(false);

Task.Factory.StartNew(async () =>
{
    Console.WriteLine("Waiting for parent task");
    await resetEvent.Wait();
    Console.WriteLine("Task #1 finished");
});

Task.Factory.StartNew(async () =>
{
    Console.WriteLine("Waiting for parent task");
    await resetEvent.Wait();
    Console.WriteLine("Task #2 finished");
});

resetEvent.Set();   //allow the tasks to complete their job
```

`AsyncAutoResetEvent` respects the same fairness policy as well as other asynchronous locks. For `AsyncManualResetEvent` it is not relevant because all suspended waiters will be released when event occurred.