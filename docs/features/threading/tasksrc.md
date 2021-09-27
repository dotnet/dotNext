ValueTask Completion Source
====
[TaskCompletionSource&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1) from .NET standard library represents the producer side of a [Task&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1). Once the source is completed, it cannot be reused. It happens because of nature of the [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task) type: task instance can be awaited multiple times. This is not true for [value task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) because it must be awaited once. See [this](https://itnext.io/why-can-a-valuetask-only-be-awaited-once-31169b324fa4) article for detailed explanation.

The fact that value task must be consumed only once is actively used by [AsyncValueTaskMethodBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncvaluetaskmethodbuilder) when constructing state machine for async methods in C#. The actual implementation of [IValueTaskSource](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.sources.ivaluetasksource) can be reused between multiple instances of [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask). When the task is completed, the builder waits for consumption of the task result. When the result is acquired, the source can be reset and placed to the pool for future use. However, .NET standard library doesn't offer this behavior as a public API.

.NEXT Threading library provides the producer side of [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) and [ValueTask&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1) suitable for pooling and reuse: [ValueTaskCompletionSource](xref:DotNext.Threading.Tasks.ValueTaskCompletionSource) and [ValueTaskCompletionSource&lt;T&gt;](xref:DotNext.Threading.Tasks.ValueTaskCompletionSource`1) respectively. In contrast to `TaskCompletionSource` type from .NET, these types can reused for multiple value tasks. As a result, you can place them to the pool and reduce memory allocation associated with tasks in async context. Additionally, these types support timeout and cancellation tracking.

Completion source can be in the following states:
* **Ready for use** means that the source can be used to obtain a fresh incompleted value task
* **Completed** means that the producer turns the source into completed state (completed successfully or with exception, canceled, timed out)
* **Consumed** means that the value task is awaited by consumer side. The source can be reused only after this event

Completion source offers the following extension points:
* `AfterConsumed` virtual method that is called automatically when the task is awaited by the consumer. You can override it to return completion source back to the pool.
* `OnTimeout` virtual method that is called when the task is timed out. The method allows to override the result to be passed to the task consumer. By default, it turns the task into failed state with [TimeoutException](https://docs.microsoft.com/en-us/dotnet/api/system.timeoutexception) exception.
* `OnCanceled` virtual method that is called when the task is canceled. The method allows to override the result to be passed to the task consumer. By default, it turns the task into failed state with [OperationCanceledException](https://docs.microsoft.com/en-us/dotnet/api/system.operationcanceledexception) exception.

`CreateTask(TimeSpan timeout, CancellationToken token)` method allows to obtain the task from the source. The method must be called only if previously produced task has been awaited and `Reset` method has been called. The produced task can be completed using `TrySetCanceled`, `TrySetException` or `TrySetResult` methods.

The common usage pattern for this kind of completion source:
1. Create or obtain an instance of completion source from the pool
1. Call `CreateTask` method (timeout and cancellation token are optional)
1. Return the task to consumer
1. Complete the task with `TrySetCanceled`, `TrySetException` or `TrySetResult` methods
1. Override `AfterConsumed` method and call `Reset` method on completion source, then return completion source back to the pool