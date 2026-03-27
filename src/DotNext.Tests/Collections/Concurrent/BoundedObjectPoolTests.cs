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
    public static async Task GetReturnConcurrently()
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
                try
                {
                    var str = pool.TryGet();
                    NotNull(str);

                    barrier.SignalAndWait(TestToken);
                    True(pool.TryReturn(str));
                }
                catch
                {
                    barrier.Dispose();
                    throw;
                }
            }
        }
    }

    [Fact]
    public static void FreezeEmptyPool()
    {
        var pool = new BoundedObjectPool<string>(4);
        pool.Freeze();
        False(pool.TryReturn("0"));
        True(pool.IsFrozen);
    }

    [Fact]
    public static void FreezeNonEmptyPool()
    {
        var pool = new BoundedObjectPool<string>(4);
        True(pool.TryReturn("0"));
        True(pool.TryReturn("1"));
        True(pool.TryReturn("2"));
        True(pool.TryReturn("3"));
        
        Same("0", pool.TryGet());
        pool.Freeze();
        Same("1", pool.TryGet());
        Same("2", pool.TryGet());
        Same("3", pool.TryGet());
        Null(pool.TryGet());
    }
    
    [Fact]
    public static async Task GetReturnFreezeConcurrently()
    {
        const int capacity = 2;
        var pool = new BoundedObjectPool<string>(capacity);
        Equal(capacity, pool.Capacity);
        True(pool.TryReturn("0"));
        True(pool.TryReturn("1"));

        var task1 = Task.Run(RentReturn, TestToken);
        var task2 = Task.Run(RentReturn, TestToken);
        await Task.Delay(100, TestToken);
        True(pool.Freeze());
        await Task.WhenAll(task1, task2);

        async Task RentReturn()
        {
            while (pool.TryGet() is { } str)
            {
                await Task.Delay(5, TestToken);
                pool.TryReturn(str);
            }
        }
    }

    [Fact]
    public static void Overflow()
    {
        var pool = new BoundedObjectPool<string>(4);
        for (var i = 0; i < pool.Capacity; i++)
        {
            True(pool.TryReturn(string.Empty));
        }
        
        False(pool.TryReturn(string.Empty));
    }
}