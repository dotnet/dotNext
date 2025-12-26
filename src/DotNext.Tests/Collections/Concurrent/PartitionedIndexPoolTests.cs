using System.Collections.Concurrent;

namespace DotNext.Collections.Concurrent;

public sealed class PartitionedIndexPoolTests : Test
{
    public static TheoryData<PartitionedIndexPool.PartitioningStrategy> Strategies =>
    [
        PartitionedIndexPool.PartitioningStrategy.Random,
        PartitionedIndexPool.PartitioningStrategy.ManagedThreadId,
    ];
    
    [Theory]
    [MemberData(nameof(Strategies))]
    public static void TakeReturn(PartitionedIndexPool.PartitioningStrategy strategy)
    {
        var pool = new PartitionedIndexPool(2, strategy);
        Equal(3 * 32, pool.Capacity);
        
        True(pool.TryTake(out var index));
        True(index.IsBetween(0.Enclosed, pool.Capacity.Disclosed));
        
        pool.Return(index);
    }
    
    [Theory]
    [MemberData(nameof(Strategies))]
    public static void TryAdd(PartitionedIndexPool.PartitioningStrategy strategy)
    {
        IProducerConsumerCollection<int> pool = new PartitionedIndexPool(3, strategy);
        False(pool.IsSynchronized);
        
        True(pool.TryTake(out var index));
        True(pool.TryAdd(index));
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public static void TakeAll(PartitionedIndexPool.PartitioningStrategy strategy)
    {
        var pool = new PartitionedIndexPool(3, strategy);
        var actual = new List<int>(pool.Capacity);

        foreach (var index in pool.TakeAll())
        {
            actual.Add(index);
        }
        
        False(pool.TryTake(out _));
        False(pool.Contains(1));

        Equal(pool.Capacity, actual.Count);

        for (var i = 0; i < pool.Capacity; i++)
        {
            Contains(i, actual);
        }
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public static void InitialState(PartitionedIndexPool.PartitioningStrategy strategy)
    {
        var pool = new PartitionedIndexPool(3, strategy);
        var actual = pool.ToArray();
        
        for (var i = 0;  i < pool.Capacity; i++)
        {
            Contains(i, actual);
        }
    }

    [Fact]
    public static void Count()
    {
        var pool = new PartitionedIndexPool(3, PartitionedIndexPool.PartitioningStrategy.ManagedThreadId);
        Equal(pool.Capacity, pool.As<IProducerConsumerCollection<int>>().Count);
    }

    [Fact]
    public static void HasElement()
    {
        var pool = new PartitionedIndexPool(3, PartitionedIndexPool.PartitioningStrategy.ManagedThreadId);
        True(pool.Contains(0));
        False(pool.Contains(int.MaxValue));
    }
}