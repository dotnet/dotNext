using System.Security.Cryptography;

namespace DotNext;

public sealed class RandomTests : Test
{
    [Fact]
    public static void RandomInt()
    {
        NotEqual(int.MaxValue, RandomNumberGenerator.Next());
    }

    [Fact]
    public static void RandomDouble()
    {
        InRange(RandomNumberGenerator.NextDouble(), 0, 1 - double.Epsilon);
    }

    [Fact]
    public static void PeekRandomFromEmptyCollection()
    {
        False(Random.Shared.Peek(Array.Empty<int>()).HasValue);
    }

    [Fact]
    public static void PeekRandomFromSingletonCollection()
    {
        Equal(5, Random.Shared.Peek([5]));
    }

    [Fact]
    public static void PeekRandomFromCollection()
    {
        IReadOnlyCollection<int> collection = [10, 20, 30];
        All(Enumerable.Range(0, collection.Count), i =>
        {
            True(Random.Shared.Peek(collection).Value is 10 or 20 or 30);
        });
    }

    [Fact]
    public static void ShuffleList()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        Random.Shared.Shuffle(list);
        NotEqual([1, 2, 3, 4, 5, 6, 7], list.ToArray());
    }

    [Fact]
    public static void TakeBoolean()
    {
        True(RandomNumberGenerator.NextBoolean(1D));
        True(Random.Shared.NextBoolean(1D));
        
        False(RandomNumberGenerator.NextBoolean(0D));
        False(Random.Shared.NextBoolean(0D));
    }
}