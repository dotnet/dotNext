Correlation Source
====
In asynchronous world sometimes you need to correlate two events logically representing the same operation. For instance, you need to communicate with remote microservice using Message Queue and identify that the incoming message is a reply to previously sent request message, and wrap this correlation into convenient asynchronous method. [AsyncCorrelationSource](xref:DotNext.Threading.AsyncCorrelationSource`2) is especially designed for that case.

The correlation contains of two steps:
* Start waiting for the signal (event) with the specified identifier
* Signal that the suspended caller from the first step can be resumed

Each event must be represented by some unique identifier used as a key. This is necessary to correlate invocation of `WaitAsync` and `TryPulse` methods.

```csharp
using DotNext.Threading;

var source = new AsyncCorrelationSource<Guid, string>(512);

// generate event id
var eventId = Guid.NewGuid();

// gets synchronization task
var task = source.WaitAsync(eventId);

// now we can send request message with the specified identifier.
// After that, we can await the task
var response = await task;

// remote party must handle request message and construct reply message with the same identifier.
// When reply message received, just inform the source about that
source.TryPulse(eventId, "Hello, world!");
```

To achieve the best performance, the following design decisions are applied to the implementation:
* Minimize mutual locking between events for better throughput. If the specified concurrency level is larger or equal to the actual number of concurrent flows then the locking is completely avoided.
* Minimize memory pressure using the pool for tasks. `WaitAsync` overloads return [ValueTask&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1) which completion source can be reused after consumption by the caller.