namespace DotNext.Collections.Concurrent;

public sealed class IndexPoolTests : Test
{
    [Fact]
    public static void CheckCapacity()
    {
        var pool = new IndexPool(4);
        True(pool.IsFull);
        Equal(4, pool.Capacity);
    }

    [Fact]
    public static void TakeAll()
    {
        var set = new HashSet<int>();
        var pool = new IndexPool(4);

        while (pool.TryGet(out var value))
        {
            set.Add(value);
        }
        
        True(pool.IsEmpty);
        Equal(set.Count, pool.Capacity);
    }
    
    [Fact]
    public static async Task StressTest()
    {
        const int capacity = 2;
        var pool = new IndexPool(capacity);
        Equal(capacity, pool.Capacity);

        using var barrier = new Barrier(capacity);
        var task1 = Task.Factory.StartNew(RentReturn, TestToken);
        var task2 = Task.Factory.StartNew(RentReturn, TestToken);
        await Task.WhenAll(task1, task2);

        void RentReturn()
        {
            for (var i = 0; i < 100; i++)
            {
                True(pool.TryGet(out var value));
                True(value is 0 or 1);

                barrier.SignalAndWait(TestToken);
                pool.Return(value);
            }
        }
    }
}