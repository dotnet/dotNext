namespace DotNext.Collections.Concurrent;

public sealed class BoundedObjectPoolTests : Test
{
    [Fact]
    public static void RentReturn()
    {
        var pool = new BoundedObjectPool<string>(2);
        True(pool.TryReturn("0"));
        True(pool.TryReturn("1"));
        False(pool.TryReturn("2"));

        Same("0", pool.TryGet());
        Same("1", pool.TryGet());
        
        True(pool.TryReturn("0"));
        True(pool.TryReturn("1"));
        False(pool.TryReturn("2"));
    }

    [Fact]
    public static async Task StressTest()
    {
        const int capacity = 2;
        var pool = new BoundedObjectPool<string>(capacity);
        Equal(capacity, pool.Capacity);
        True(pool.TryReturn("0"));
        True(pool.TryReturn("1"));

        using var barrier = new Barrier(capacity);
        var task1 = Task.Factory.StartNew(RentReturn, TestToken);
        var task2 = Task.Factory.StartNew(RentReturn, TestToken);
        await Task.WhenAll(task1, task2);

        void RentReturn()
        {
            for (var i = 0; i < 100; i++)
            {
                var str = pool.TryGet();
                NotNull(str);
                
                barrier.SignalAndWait(TestToken);
                True(pool.TryReturn(str));
            }
        }
    }
}