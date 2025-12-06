namespace DotNext.Threading.Tasks;

[Collection(TestCollections.AdvancedSynchronization)]
public class TaskQueueTests : Test
{
    [Fact]
    public static async Task EmptyQueue()
    {
        var queue = new TaskQueue<Task>(10);
        True(queue.CanEnqueue);
        Null(queue.HeadTask);
        False(queue.TryDequeue(out _));
        Null(await queue.TryDequeueAsync(TestToken));
    }

    [Fact]
    public static async Task QueueOverflow()
    {
        var queue = new TaskQueue<Task>(3);
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));
        False(queue.CanEnqueue);
        NotNull(queue.HeadTask);

        var enqueueTask = queue.EnqueueAsync(Task.CompletedTask, TestToken).AsTask();
        False(enqueueTask.IsCompleted);

        True(queue.TryDequeue(out var task));
        True(task.IsCompleted);

        await enqueueTask.WaitAsync(TestToken);
        queue.Clear();
    }

    [Fact]
    public static async Task EnumerateCompletedTasks()
    {
        var queue = new TaskQueue<Task>(3);
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));

        var count = 0;
        await foreach (var task in queue)
        {
            Same(Task.CompletedTask, task);
            count++;
        }

        Equal(3, count);
    }
    
    [Fact]
    public static async Task TryDequeueCompletedTasks()
    {
        var queue = new TaskQueue<Task>(3);
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));

        var count = 0;
        while (await queue.TryDequeueAsync(TestToken) is { } task)
        {
            Same(Task.CompletedTask, task);
            count++;
        }

        Equal(3, count);
    }

    [Fact]
    public static async Task EnumerateTasks()
    {
        var queue = new TaskQueue<Task>(3);
        await queue.EnqueueAsync(Task.Delay(10, TestToken), TestToken);
        await queue.EnqueueAsync(Task.Delay(15, TestToken), TestToken);
        await queue.EnqueueAsync(Task.Delay(20, TestToken), TestToken);

        var count = 0;
        await foreach (var task in queue)
        {
            True(task.IsCompleted);
            count++;
        }

        Equal(3, count);
    }

    [Fact]
    public static async Task DelayedDequeue()
    {
        var queue = new TaskQueue<Task>(3);
        var enqueueTask = queue.DequeueAsync(TestToken).AsTask();
        False(enqueueTask.IsCompleted);

        True(queue.TryEnqueue(Task.CompletedTask));

        await enqueueTask.WaitAsync(TestToken);
        Null(await queue.TryDequeueAsync(TestToken));
    }

    [Fact]
    public static async Task DequeueCancellation()
    {
        var source = new TaskCompletionSource();
        var queue = new TaskQueue<Task>(3);
        True(queue.TryEnqueue(source.Task));

        await ThrowsAnyAsync<OperationCanceledException>(queue.DequeueAsync(new(canceled: true)).AsTask);
    }

    [Fact]
    public static async Task FailedTask()
    {
        var source = new TaskCompletionSource();
        var queue = new TaskQueue<Task>(3);
        True(queue.TryEnqueue(source.Task));

        var dequeueTask = queue.DequeueAsync(TestToken).AsTask();
        False(dequeueTask.IsCompleted);

        source.SetException(new Exception());
        Same(source.Task, await dequeueTask);
    }

    [Fact]
    public static async Task EnsureFreeSpace()
    {
        var queue = new TaskQueue<Task>(3);
        await queue.EnsureFreeSpaceAsync(TestToken);
        
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));
        True(queue.TryEnqueue(Task.CompletedTask));

        var task = queue.EnsureFreeSpaceAsync(TestToken).AsTask();
        False(task.IsCompleted);

        True(queue.TryDequeue(out _));
        await task.WaitAsync(TestToken);
    }
}