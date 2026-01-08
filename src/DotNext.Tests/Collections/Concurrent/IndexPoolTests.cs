using System.Collections.Concurrent;

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
        Equal(0, pool.GetEnumerator().RemainingCount);
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
        Equal(pool.Count, pool.GetEnumerator().RemainingCount);

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
        expected.ForEach(static (element, index) => element.Value = index);

        Equal(expected, pool.ToArray());

        while (pool.TryTake(out _))
        {
            // take all indices
        }

        Equal(Array.Empty<int>(), pool.ToArray());
    }

    [Fact]
    public static void CustomMaxValue()
    {
        var pool = new IndexPool(maxValue: 2);
        Equal(3, pool.Count);

        Equal(0, pool.Take());
        Equal(2, pool.Count);
        
        Equal(1, pool.Take());
        Single(pool);
        
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
    public static void TakeAllAndReturn()
    {
        var pool = new IndexPool { IsEmpty = true };
        pool.Return(0);
        pool.Return(2);
        False(pool.IsEmpty);

        var copy = pool.TakeAll();
        Empty(pool);

        Contains(0, copy);
        Contains(2, copy);
        DoesNotContain(1, copy);

        Span<int> span = stackalloc int[copy.Count];
        Equal(copy.Count, copy.CopyTo(span));
        pool.Return(span);
        Contains(0, pool);
        Contains(2, pool);
    }

    [Fact]
    public static void TryAdd()
    {
        IProducerConsumerCollection<int> pool = new IndexPool { IsEmpty = true };
        False(pool.IsSynchronized);
        
        True(pool.TryAdd(0));
        True(pool.TryAdd(1));
        Contains(0, pool);
        Contains(1, pool);
        
        False(pool.TryAdd(int.MaxValue));
        False(pool.TryAdd(0));
        False(pool.TryAdd(1));
    }
}