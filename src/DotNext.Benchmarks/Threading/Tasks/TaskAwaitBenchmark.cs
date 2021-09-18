using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 10)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class TaskAwaitBenchmark
{
    private readonly Task task = Task.FromResult("Hello, world");

    [Benchmark]
    public async ValueTask<string> AwaitUsingDLR()
    {
        dynamic task = this.task;
        return await task.ConfigureAwait(false);
    }

    [Benchmark]
    public async ValueTask<string> AwaitUsingDynamicTaskAwaitable()
        => await task.AsDynamic().ConfigureAwait(false);
}