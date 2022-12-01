using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace DotNext.Threading.Tasks;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ChannelVersusPipeBenchmark
{
    [Params(10, 100, 1000)]
    public int iterations;

    private static async Task<int> GetTask(int i)
    {
        await Task.Yield();
        return i;
    }

    [Benchmark(Baseline = true)]
    public async Task ProduceConsumeUnboundedChannel()
    {
        var channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleWriter = true, SingleReader = true });

        Task<int> consumer = Task.Run(async () =>
        {
            var sum = 0;
            await foreach (var item in channel.Reader.ReadAllAsync())
                sum += item;

            return sum;
        });

        for (var i = 0; i < iterations; i++)
            channel.Writer.TryWrite(await GetTask(i));

        channel.Writer.Complete();

        await consumer;
    }

    [Benchmark]
    public async Task ProduceConsumeCompletionPipe()
    {
        var pipe = new TaskCompletionPipe<Task<int>>();

        Task<int> consumer = Task.Run(async () =>
        {
            var sum = 0;
            await foreach (var item in pipe.GetConsumer())
                sum += item;

            return sum;
        });

        for (var i = 0; i < iterations; i++)
            pipe.Add(GetTask(i));

        pipe.Complete();

        await consumer;
    }
}