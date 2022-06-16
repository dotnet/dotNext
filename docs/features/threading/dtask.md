Delayed Tasks
====
[Scheduler](xref:DotNext.Threading.Scheduler) class provides a way to schedule asynchronous tasks execution delayed in time. Scheduled task can be canceled in two states:
* If the task is scheduled but not yet executed
* During task execution

The following example demonstrates a task to be executed asynchronously after 1 minute:
```csharp
using DotNext.Threading;

Scheduler.DelayedTask delayed = Scheduler.ScheduleAsync(ExecuteAsync, "Task1", TimeSpan.FromMinutes(1));

// await the task
await delayed.Task;

static async ValueTask ExecuteAsync(string taskName, CancellationToken token)
{
    await Task.Yield();
    Console.WriteLine($"Hello from delayed task {taskName}!");
}
```

[DelayedTask](xref:DotNext.Threading.Scheduler.DelayedTask) type provides control over the scheduled task such as cancellation.