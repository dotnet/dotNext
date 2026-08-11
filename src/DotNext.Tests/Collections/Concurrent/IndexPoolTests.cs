namespace DotNext.Collections.Concurrent;

public sealed class IndexPoolTests : Test
{
    [Fact]
    public static void CheckCapacity()
    {
        var pool = new IndexPool(4);
        Equal(5, pool.Capacity);
    }

    [Fact]
    public static void TakeAll()
    {
        var set = new HashSet<int>();
        var pool = new IndexPool(4);
        False(pool.IsEmpty);

        while (pool.TryGet(out var value))
        {
            set.Add(value);
        }
        
        Equal(set.Count, pool.Capacity);
        True(pool.IsEmpty);
    }
    
    [Fact]
    public static async Task StressTest()
    {
        const int capacity = 2;
        var pool = new IndexPool(capacity);
        Equal(capacity + 1, pool.Capacity);

        using var barrier = new Barrier(pool.Capacity);
        var task1 = Task.Factory.StartNew(RentReturn, TestToken);
        var task2 = Task.Factory.StartNew(RentReturn, TestToken);
        var task3 = Task.Factory.StartNew(RentReturn, TestToken);
        await Task.WhenAll(task1, task2, task3);

        void RentReturn()
        {
            for (var i = 0; i < 100; i++)
            {
                int value;
                try
                {
                    True(pool.TryGet(out value));
                    True(value is 0 or 1 or 2);

                    barrier.SignalAndWait(TestToken);
                }
                catch
                {
                    barrier.Dispose();
                    throw;
                }

                pool.Return(value);
            }
        }
    }

    [Fact]
    public static void FastItem()
    {
        var pool = new IndexPool(4);
        True(pool.TryGet(out var value));
        Equal(0, value);
        
        True(pool.TryReturn(value));
        True(pool.TryGet(out value));
        Equal(0, value);
    }
}