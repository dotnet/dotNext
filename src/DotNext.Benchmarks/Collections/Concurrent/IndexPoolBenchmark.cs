using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Collections.Concurrent;

namespace DotNext.Collections.Concurrent;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class IndexPoolBenchmark
{
    private const int PoolSize = 128;
    private readonly ConcurrentQueue<object> bag = new(CreateObjects());
    private readonly object[] objects = CreateObjects();
    private readonly IndexPool pool = new(PoolSize);

    [Benchmark(Description = "IndexPool Take/Return", Baseline = true)]
    public int IndexPoolTakeReturn()
    {
        pool.TryGet(out var index);
        var hashCode = objects[index].GetHashCode();

        pool.Return(index);
        return hashCode;
    }

    [Benchmark(Description = "ConcurrentBag Take/Return")]
    public int ConcurrentBagTakeReturn()
    {
        bag.TryDequeue(out var obj);
        var hashCode = obj.GetHashCode();

        bag.Enqueue(obj);
        return hashCode;
    }

    private static object[] CreateObjects()
    {
        var result = new object[PoolSize];
        result.Initialize<object>();
        return result;
    }
}