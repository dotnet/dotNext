namespace DotNext.Collections.Concurrent;

public sealed class IndexPoolTests : Test
{
    [Fact]
    public static void EmptyPool()
    {
        var pool = default(IndexPool);
        False(pool.TryPeek(out _));
        False(pool.TryTake(out _));
        DoesNotContain(10, pool);
        Empty(pool);
    }

    [Fact]
    public static void EmptyPool2()
    {
        var pool = new IndexPool() { IsEmpty = true };
        False(pool.TryPeek(out _));
        False(pool.TryTake(out _));
        DoesNotContain(10, pool);
        Empty(pool);

        pool.Reset();
        NotEmpty(pool);
        Contains(10, pool);
    }

    [Fact]
    public static void TakeAll()
    {
        var pool = new IndexPool();
        NotEmpty(pool);

        for (var i = 0; i <= IndexPool.MaxValue; i++)
        {
            Equal(i, pool.Take());
        }

        Throws<OverflowException>(() => pool.Take());
    }

    [Fact]
    public static void ContainsAll()
    {
        var pool = new IndexPool();
        for (var i = 0; i <= IndexPool.MaxValue; i++)
        {
            True(pool.Contains(i));
        }

        for (var i = 0; i <= IndexPool.MaxValue; i++)
        {
            Equal(i, pool.Take());
        }

        for (var i = 0; i <= IndexPool.MaxValue; i++)
        {
            False(pool.Contains(i));
        }
    }

    [Fact]
    public static void Enumerator()
    {
        var pool = new IndexPool();
        var expected = new int[pool.Count];
        Span.ForEach(expected, static (ref int value, int index) => value = index);

        Equal(expected, pool.ToArray());

        while (pool.TryTake(out _))
        {
            // take all indicies
        }

        Equal(Array.Empty<int>(), pool.ToArray());
    }

    [Fact]
    public static void CustomMaxValue()
    {
        var pool = new IndexPool(maxValue: 2);
        Equal(3, pool.Count);

        Equal(0, pool.Take());
        Equal(1, pool.Take());
        Equal(2, pool.Take());

        False(pool.TryTake(out _));
        Empty(pool);
    }

    [Fact]
    public static void Consistency()
    {
        var pool = new IndexPool();
        Equal(0, pool.Take());

        Equal(1, pool.Take());
        pool.Return(1);

        Equal(1, pool.Take());
        pool.Return(1);

        pool.Return(0);
    }

    [Fact]
    public static void TakeReturnMany()
    {
        var pool = new IndexPool();
        Span<int> indicies = stackalloc int[IndexPool.Capacity];

        Equal(IndexPool.Capacity, pool.Take(indicies));
        Empty(pool);

        pool.Return(indicies);
        NotEmpty(pool);
    }
}