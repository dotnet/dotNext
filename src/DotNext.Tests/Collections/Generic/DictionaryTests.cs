using System.Collections.Generic;
using Xunit;

namespace DotNext.Collections.Generic
{
    public sealed class DictionaryTests : Assert
    {
        [Fact]
        public static void ReadOnlyIndexer()
        {
            IReadOnlyDictionary<string, int> dict = new Dictionary<string, int>()
            {
                {"one", 1},
                {"two", 2}
            };
            Equal(1, Dictionary.Indexer<string, int>.ReadOnly.Invoke(dict, "one"));
            Equal(2, dict.IndexerGetter().Invoke("two"));
        }

        [Fact]
        public static void Indexer()
        {
            IDictionary<string, int> dict = new Dictionary<string, int>()
            {
                {"one", 1},
                {"two", 2}
            };
            Equal(1, Dictionary.Indexer<string, int>.Getter.Invoke(dict, "one"));
            Equal(2, dict.IndexerGetter().Invoke("two"));
            dict.IndexerSetter().Invoke("two", 3);
            Equal(3, dict.IndexerGetter().Invoke("two"));
            Dictionary.Indexer<string, int>.Setter.Invoke(dict, "three", 3);
            Equal(3, dict.IndexerGetter().Invoke("three"));
        }

        [Fact]
        public void ConversionTest()
        {
            var dict = new Dictionary<string, int>()
            {
                {"a", 1 },
                {"b", 2 }
            };
            var view = dict.ConvertValues(i => i + 10);
            Equal(11, view["a"]);
            Equal(12, view["b"]);
        }
    }
}
