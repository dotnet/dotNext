GC Notifications
====

.NET BCL provides [API](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/notifications) for receiving notifications from GC. It has the following disadvantages:
1. It is blocking API
1. It allows to receive notifications from full blocking GC only

[GCNotification](xref:DotNext.Runtime.GCNotification) from .NEXT library class exposes a way to receive GC notifications asynchronously.

> [!NOTE]
> Garbage Collection Notifications API from .NET provides ability to wait for GC approach and GC completion. GC Asynchronous Notifications from .NEXT library can be received only asynchronously after beginning of GC.

**GC Asynchronous Notifications** are asynchronous by nature. When actual GC occurs, the infrastructure schedules the notification using .NET Thread Pool for further delivery to the receiver. As a result, the delay between sending and receiving notification is possible.

[GCNotification](xref:DotNext.Runtime.GCNotification) class allows to receive the notification in the following ways:
* Through registered callback
* Using **await** operator

The following example demonstrates detection of a single GC occurred in the process:
```csharp
using System;
using System.Threading;
using DotNext.Runtime;

GCMemoryInfo info = await GCNOtification.GCTriggered().WaitAsync(CancellationToken.None);
Console.WriteLine("GC occurred");
```

The notification object carries information about the triggered GC in the form of [GCMemoryInfo](https://docs.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo) value type.

GC Notifications can be combined using logical operators: AND, OR, NOT, XOR:
```csharp
using System;
using System.Threading;
using DotNext.Runtime;

// detect GC for generation 0 or 1, but not for 2
GCMemoryInfo info = await (!GCNOtification.GCTriggered(2)).WaitAsync(CancellationToken.None);
Console.WriteLine("GC occurred");
```

[GCNotification](xref:DotNext.Runtime.GCNotification) provides a rich set of predefined filters to capture various GC events. For instance, it is possible to detect GC memory pressure:
```csharp
using System;
using System.Threading;
using DotNext.Runtime;

// detect 80% memory occupation (GC after cleanup has less than 20% of free memory)
using GCNotification.Registration reg = GCNotification.MemoryThreshold(0.8).Register(GCCallback, "Memory pressure detected");
GC.Collect();

static void GCCallback(string message, GCMemoryInfo info)
    => Console.WriteLine(message);
```

The primary consumers of a new API are object pools, buffers, in-memory caches, connection pools. For instance, on high memory pressure you can drain in-memory cache.