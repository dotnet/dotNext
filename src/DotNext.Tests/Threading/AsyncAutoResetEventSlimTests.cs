namespace DotNext.Threading;

using Diagnostics;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncAutoResetEventSlimTests : Test
{
    [Fact]
    public static async Task Concurrency()
    {
        var ev = new AsyncAutoResetEventSlim();
        var start = new Timestamp();

        var producer = Task.Run(() =>
        {
            while (start.Elapsed < TimeSpan.FromSeconds(1))
                ev.Set();
        }, TestToken);

        var consumer = Task.Run(async () =>
        {
            while (!producer.IsCompleted)
                await ev.WaitAsync();
        }, TestToken);

        await producer;
        ev.Set();
        await consumer;
    }

    [Fact]
    public static async Task SetResult()
    {
        var ev = new AsyncAutoResetEventSlim(initialState: true);
        True(await ev.WaitAsync());

        // signaled before wait
        ev.Set(false);
        False(await ev.WaitAsync());

        // last result wins
        ev.Set(false);
        ev.Set(true);
        True(await ev.WaitAsync());

        // signaled after wait
        var task = ev.WaitAsync();
        False(task.IsCompleted);
        ev.Set(false);
        False(await task);
    }

    [Fact]
    public static async Task InitialState()
    {
        var ev = new AsyncAutoResetEventSlim();
        var task = ev.WaitAsync().AsTask();
        False(task.IsCompleted);
        ev.Set();
        True(await task);

        ev = new(initialState: true);
        True(ev.WaitAsync().IsCompletedSuccessfully);
    }
}