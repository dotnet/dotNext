using System.Collections.Generic;
using Xunit;

namespace DotNext.Collections.Generic
{
    public sealed class CollectionTests: Assert
    {
        [Fact]
        public void ReadOnlyIndexerTest()
        {
            IReadOnlyList<long> array = new long[] {5L, 6L, 20L };
            Equal(20L, List.Indexer<long>.ReadOnly(array, 2));
            Equal(6L, array.IndexerGetter().Invoke(1));
        }

        [Fact]
        public void IndexerTest()
        {
            IList<long> array = new long[] { 5L, 6L, 30L };
            Equal(30L, List.Indexer<long>.Getter(array, 2));
            List.Indexer<long>.Setter(array, 1, 10L);
            Equal(10L, array.IndexerGetter().Invoke(1));
            array.IndexerSetter().Invoke(0, 6L);
            Equal(6L, array.IndexerGetter().Invoke(0));
        }
    }
}
