Task Completion Pipe
====
`WhenAll` static method of [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task) waits until all passed tasks will be completed. Thus, the maximum possible execution time of such task is equal to the slowest task in the group. It means that there is no way to obtain results from the faster tasks in the group. From other side, `WhenAny` static method allows to obtain the result of the fastest task in the group. However, it doesn't allow to wait for all tasks. In some use cases, responsiveness of the program is a critical requirement so we need to have an ability to obtain and process tasks asynchronously as they complete.

[TaskCompletionPipe&lt;T&gt;](xref:DotNext.Threading.Tasks.TaskCompletionPipe`1) specially designed to process tasks as the complete using [asynchronous streams](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/generate-consume-asynchronous-stream). It provides the following API surface:
* Producer side:
    * `Add` method to add asynchronous tasks
    * `Complete` method to inform the pipe that newly tasks will not be added
* Consumer side:
    * [IAsyncEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1) interface implementation allows to use **await foreach** statement in C# programming language to consume tasks asynchronously as they complete
    * `TryRead` method attempts to obtain the completed task synchronously
    * `WaitToReadAsync` allows to wait to the completed task submitted to the pipe. It should be used in combination with `TryRead` to optimize memory allocations by consumer
    * `Completion` property returns a task that completes when no more tasks will ever be submitted to the pipe

> [!NOTE]
> Performance tip: `TryRead` and `WaitToReadAsync` methods are preferred way if consumer processes completed tasks slower than producer submits new tasks. Otherwise, **await foreach** is preferred.

The pipe is thread-safe for both consumer and producer. Moreover, multiple consumers and multiple producers are allowed, no need to specify configuration properties like [SingleReader](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channeloptions.singlereader) as for [Channel&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1) class.