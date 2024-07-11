namespace DotNext.Threading.Tasks;

using static Collections.Generic.AsyncEnumerable;

public class TaskCompletionPipeTests : Test
{
    [Fact]
    public static async Task StressTest()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        for (var i = 0; i < 100; i++)
        {
            pipe.Add(Task.Run<int>(async () =>
            {
                await Task.Delay(Random.Shared.Next(10, 100));
                return 42;
            }));
        }

        pipe.Complete();

        var result = 0;
        await foreach (var task in pipe)
        {
            True(task.IsCompleted);
            result += await task;
        }

        Equal(4200, result);
    }

    [Fact]
    public static async Task StressTest2()
    {
        var expectedUserData = new object();
        var pipe = new TaskCompletionPipe<Task<int>>();
        for (var i = 0; i < 100; i++)
        {
            pipe.Add(Task.Run<int>(async () =>
            {
                await Task.Delay(Random.Shared.Next(10, 100));
                return 42;
            }),
            expectedUserData);
        }

        pipe.Complete();

        var result = 0;
        while (await pipe.WaitToReadAsync())
        {
            if (pipe.TryRead(out var task, out var actualUserData))
            {
                True(task.IsCompleted);
                result += await task;
                Same(expectedUserData, actualUserData);
            }
        }

        Equal(4200, result);
    }

    [Fact]
    public static async Task ConsumerAfterAddingButBeforeCompletion()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        pipe.Add(Task.FromResult(1));
        var consumer = pipe.GetConsumer().GetAsyncEnumerator();
        True(await consumer.MoveNextAsync());

        var t = consumer.MoveNextAsync().AsTask();
        pipe.Complete();

        False(await t);
    }

    [Fact]
    public static async Task QueueGrowth()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        pipe.Add(Task.FromResult(42));
        pipe.Add(Task.FromResult(43));
        True(await pipe.WaitToReadAsync());
        True(pipe.TryRead(out var task));
        Equal(42, await task);

        pipe.Add(Task.FromResult(44));
        pipe.Add(Task.FromResult(45));
        pipe.Add(Task.FromResult(46));

        await using (var enumerator = pipe.GetAsyncEnumerator(CancellationToken.None))
        {
            True(await enumerator.MoveNextAsync());
            Equal(43, await enumerator.Current);

            True(await enumerator.MoveNextAsync());
            Equal(44, await enumerator.Current);

            True(await enumerator.MoveNextAsync());
            Equal(45, await enumerator.Current);

            True(await enumerator.MoveNextAsync());
            Equal(46, await enumerator.Current);
        }

        False(pipe.TryRead(out task));
    }

    [Fact]
    public static async Task ResetWhenScheduled()
    {
        var pipe = new TaskCompletionPipe<Task>();
        var source = new TaskCompletionSource();
        pipe.Add(source.Task);

        pipe.Reset();
        pipe.Complete();
        source.SetResult();
        False(await pipe.WaitToReadAsync());
    }

    [Fact]
    public static async Task ConsumePipe()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        pipe.Add(Task.Run(Func.Constant(42)));
        pipe.Add(Task.Run(Func.Constant(43)));
        pipe.Add(Task.Run(Func.Constant(44)));
        pipe.Complete();

        var array = await pipe.GetConsumer().ToArrayAsync(initialCapacity: 3);
        Contains(42, array);
        Contains(43, array);
        Contains(44, array);
    }

    [Fact]
    public static async Task TooManyConsumers()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        var consumer1 = pipe.WaitToReadAsync().AsTask();
        var consumer2 = pipe.WaitToReadAsync().AsTask();

        pipe.Add(Task.FromResult(42));
        pipe.Complete();
        True(await consumer1);
        False(await consumer2);
        True(await pipe.WaitToReadAsync());
    }

    [Fact]
    public static async Task WrongIteratorVersion()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();
        await using var enumerator = pipe.GetAsyncEnumerator(CancellationToken.None);
        pipe.Reset();

        pipe.Add(Task.FromResult(42));
        False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public static async Task CompletedTaskGroupToCollection()
    {
        await foreach (var t in TaskCompletionPipe.Create([Task.CompletedTask, Task.CompletedTask]))
        {
            True(t.IsCompleted);
        }
    }

    [Fact]
    public static async Task TaskGroupToCollection()
    {
        var source1 = new TaskCompletionSource<int>();
        var source2 = new TaskCompletionSource<int>();
        await using var consumer = TaskCompletionPipe.GetConsumer([source1.Task, source2.Task]).GetAsyncEnumerator();
        
        source1.SetResult(42);
        True(await consumer.MoveNextAsync());
        Equal(42, consumer.Current);
        
        source2.SetResult(43);
        True(await consumer.MoveNextAsync());
        Equal(43, consumer.Current);
        
        False(await consumer.MoveNextAsync());
    }

    [Fact]
    public static async Task CompletionTask()
    {
        var pipe = new TaskCompletionPipe<Task> { IsCompletionTaskSupported = true };
        True(pipe.IsCompletionTaskSupported);
        
        var source1 = new TaskCompletionSource();
        var source2 = new TaskCompletionSource();

        pipe.Add([source1.Task, source2.Task], complete: true);

        source1.SetResult();
        source2.SetResult();

        await pipe.Completion.WaitAsync(DefaultTimeout);

        var count = 0;
        while (pipe.TryRead(out _))
        {
            count++;
        }

        Equal(2, count);
    }
}