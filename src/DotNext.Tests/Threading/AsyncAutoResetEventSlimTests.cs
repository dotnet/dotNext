namespace DotNext.Threading;

using Diagnostics;

public sealed class AsyncAutoResetEventSlimTests : Test
{
    [Fact]
    public static async Task Concurrency()
    {
        var ev = new AsyncAutoResetEventSlim(false);
        var start = new Timestamp();

        var producer = Task.Run(() =>
        {
            while (start.Elapsed < TimeSpan.FromSeconds(1))
                ev.Set();
        });

        var consumer = Task.Run(async () =>
        {
            while (!producer.IsCompleted)
                await ev.WaitAsync();
        });

        await producer;
        ev.Set();
        await consumer;
    }
}