using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Collections.Concurrent;

namespace DotNext.Collections.Concurrent;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class IndexPoolBenchmark
{
    private readonly ConcurrentBag<object> bag = new(CreateObjects());
    private readonly object[] objects = CreateObjects();
    private IndexPool pool = new();

    [Benchmark(Description = "IndexPool Take/Return", Baseline = true)]
    public int IndexPoolTakeReturn()
    {
        pool.TryTake(out var index);
        var hashCode = objects[index].GetHashCode();

        pool.Return(index);
        return hashCode;
    }

    [Benchmark(Description = "ConcurrentBag Take/Return")]
    public int ConcurrentBagTakeReturn()
    {
        bag.TryTake(out var obj);
        var hashCode = obj.GetHashCode();

        bag.Add(obj);
        return hashCode;
    }

    private static object[] CreateObjects()
    {
        var result = new object[64];
        Span.Initialize<object>(result);
        return result;
    }
}