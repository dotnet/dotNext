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
}