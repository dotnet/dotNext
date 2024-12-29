namespace DotNext.Collections.Generic;

[TestClass]
public class ListTests
{
    [TestMethod]
    public void Indexer()
    {
        IList<long> array = [5L, 6L, 30L];
        Assert.AreEqual(30L, List.Indexer<long>.Getter(array, 2));
        List.Indexer<long>.Setter(array, 1, 10L);
        Assert.AreEqual(10L, array.IndexerGetter().Invoke(1));
        array.IndexerSetter().Invoke(0, 6L);
        Assert.AreEqual(6L, array.IndexerGetter().Invoke(0));
    }
    
    [TestMethod]
    public void ReadOnlyIndexer()
    {
        IReadOnlyList<long> array = [5L, 6L, 20L];
        Assert.AreEqual(20L, List.Indexer<long>.ReadOnly(array, 2));
        Assert.AreEqual(6L, array.IndexerGetter().Invoke(1));
    }
}