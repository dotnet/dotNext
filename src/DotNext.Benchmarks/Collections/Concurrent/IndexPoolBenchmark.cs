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
    private readonly ConcurrentBag<object> bag = new(CreateObjects());
    private readonly object[] objects = CreateObjects();
    private IndexPool pool = new();
    private PartitionedIndexPool partitionedByThreadId = new(2, PartitionedIndexPool.PartitioningStrategy.ManagedThreadId);
    private PartitionedIndexPool partitionedRandomly = new(2, PartitionedIndexPool.PartitioningStrategy.Random);

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

    [Benchmark(Description = "PartitionedIndexPool(ThreadId) Take/Return")]
    public int PartitionedByThreadIdTakeReturn()
    {
        partitionedByThreadId.TryTake(out var index);
        var hashCode = objects[index].GetHashCode();

        partitionedByThreadId.Return(index);
        return hashCode;
    }
    
    [Benchmark(Description = "PartitionedIndexPool(Random) Take/Return")]
    public int PartitionedRandomlyTakeReturn()
    {
        partitionedRandomly.TryTake(out var index);
        var hashCode = objects[index].GetHashCode();

        partitionedRandomly.Return(index);
        return hashCode;
    }

    private static object[] CreateObjects()
    {
        var result = new object[PoolSize];
        result.Initialize<object>();
        return result;
    }
}