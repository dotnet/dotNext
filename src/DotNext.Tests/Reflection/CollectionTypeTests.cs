using System.Collections;

namespace DotNext.Reflection;

public sealed class CollectionTypeTests : Test
{
    [Fact]
    public static void GetItemTypeTest()
    {
        Equal(typeof(long), typeof(long[]).ItemType);
        Equal(typeof(bool), typeof(IList<bool>).ItemType);
        Equal(typeof(object), typeof(IEnumerable).ItemType);
        Equal(typeof(int), typeof(IAsyncEnumerable<int>).ItemType);
    }
    
    [Fact]
    public static void ReadOnlyIndexer()
    {
        IReadOnlyList<long> array = [5L, 6L, 20L];
        Equal(20L, IReadOnlyList<long>.Indexer(array, 2));
    }

    [Fact]
    public static void Indexer()
    {
        IList<long> array = [5L, 6L, 30L];
        Equal(30L, IList<long>.IndexerGetter(array, 2));
        IList<long>.IndexerSetter(array, 1, 10L);
    }
}