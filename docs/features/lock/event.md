Async Reset Event
====
[AsyncResetEvent](../../api/DotNext.Threading.AsyncResetEvent.yml) is an asynchronous alternative to [ManualResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent) and [AutoResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent) with the same semantics. However, it doesn't support interoperation with .NET thread pool and native code because there is no wait handle associated with an instance of event.

```csharp
using DotNext.Threading;
using System;
using System.Threading;

var resetEvent = new AsyncResetEvent(false, EventResetMode.ManualReset);

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

This class implements the same fairness policy as well as other asynchronous locks.