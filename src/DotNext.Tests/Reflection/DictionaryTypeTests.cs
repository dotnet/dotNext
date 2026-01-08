namespace DotNext.Reflection;

public sealed class DictionaryTypeTests : Test
{
    [Fact]
    public static void ReadOnlyIndexer()
    {
        IReadOnlyDictionary<string, int> dict = new Dictionary<string, int>()
        {
            {"one", 1},
            {"two", 2}
        };
        Equal(1, IReadOnlyDictionary<string, int>.Indexer.Invoke(dict, "one"));
    }
    
    [Fact]
    public static void Indexer()
    {
        IDictionary<string, int> dict = new Dictionary<string, int>()
        {
            {"one", 1},
            {"two", 2}
        };
        Equal(1, IDictionary<string, int>.IndexerGetter.Invoke(dict, "one"));
        IDictionary<string, int>.IndexerSetter.Invoke(dict, "three", 3);
        Equal(3, IDictionary<string, int>.IndexerGetter.Invoke(dict, "three"));
    }
}