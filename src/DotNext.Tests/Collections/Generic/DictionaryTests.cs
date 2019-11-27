using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Collections.Generic
{
    [ExcludeFromCodeCoverage]
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
        public static void ConversionTest()
        {
            var dict = new Dictionary<string, int>
            {
                {"a", 1 },
                {"b", 2 }
            };
            var view = dict.ConvertValues(i => i + 10);
            Equal(11, view["a"]);
            Equal(12, view["b"]);
        }

        [Fact]
        public static void ReadOnlyView()
        {
            var dict = new Dictionary<string, string>
            {
                {"one", "1" },
                {"two", "2" }
            };
            var view = new ReadOnlyDictionaryView<string, string, int>(dict, new ValueFunc<string, int>(int.Parse));
            Equal(1, view["one"]);
            Equal(2, view["two"]);
            True(view.TryGetValue("one", out var i));
            Equal(1, i);
            False(view.TryGetValue("three", out i));
            False(view.ContainsKey("three"));
            True(view.ContainsKey("two"));
            foreach (var (key, value) in view)
                if (!value.Between(0, 2, BoundType.Closed))
                    throw new Exception();
        }

        [Fact]
        public static void ForEachPair()
        {
            var dict = new Dictionary<string, int>
            {
                {"1", 1 },
                {"2", 2 }
            };
            dict.ForEach((key, value) =>
            {
                switch (key)
                {
                    case "1":
                        Equal(1, value);
                        break;
                    case "2":
                        Equal(2, value);
                        break;
                }
            });
        }

        [Fact]
        public static void GetOrAddPair()
        {
            var dict = new Dictionary<int, string>
            {
                {1, "One" },
                {2, "Two" }
            };
            Equal("One", dict.GetOrAdd(1, "Three"));
            Equal("Three", dict.GetOrAdd(3, "Three"));
            Equal("Two", dict.GetOrAdd(2, key => "Three"));
            Equal("Four", dict.GetOrAdd(4, key => "Four"));
            Equal("One", dict.GetOrInvoke(1, () => "Two"));
            Equal("Alt", dict.GetOrInvoke(10, () => "Alt"));
        }
    }
}
