Event Hub
====
[AsyncEventHub](xref:DotNext.Threading.AsyncEventHub) provides a way to synchronize a group of asynchronous events. Under the hood, it manages a collection of [TaskCompletionSource](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource) objects. It is possible to synchronize one or more events in various ways:
* Wait for single specific event
* Wait for any of the specified events (_OR_ gateway)
* Wait for all events from the specified subset of events (_AND_ gateway)

There are two types of signals:
* _Pulse_ turns the specified event into signaled state
* _Reset and Pulse_ turns the specified event into signaled state and switches all other events back to non-signaled state

Typically, Event Hub is applied to synchronize transition between phases in asynchronous process.

# Differences with AsyncCorrelationSource
[AsyncCorrelationSource](xref:DotNext.Threading.AsyncCorrelationSource`2) allows to coordinate multiple unique events such as asynchronous messages. On the other hand, [AsyncEventHub](xref:DotNext.Threading.AsyncEventHub) provides a synchronization of a known group of asynchronous events.