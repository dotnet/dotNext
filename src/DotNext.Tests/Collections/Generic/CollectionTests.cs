using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Generic
{
    [ExcludeFromCodeCoverage]
    public sealed class CollectionTests : Test
    {
        [Fact]
        public static void AddingItems()
        {
            var list = new List<int>();
            list.AddAll(new[] { 1, 3, 5 });
            Equal(1, list[0]);
            Equal(3, list[1]);
            Equal(5, list[2]);
        }

        [Fact]
        public static void LinkedListToArray()
        {
            var list = new LinkedList<int>();
            list.AddLast(10);
            list.AddLast(20);
            list.AddLast(30);

            ICollection<int> collection = list;
            Equal(new[] { 10, 20, 30 }, Generic.Collection.ToArray(collection));

            IReadOnlyCollection<int> collection2 = list;
            Equal(new[] { 10, 20, 30 }, Generic.Collection.ToArray(collection2));
        }

        [Fact]
        public static void ReadOnlyView()
        {
            var view = new ReadOnlyCollectionView<string, int>(new[] { "1", "2", "3" }, new Converter<string, int>(int.Parse));
            Equal(3, view.Count);
            NotEmpty(view);
            All(view, static value => True(value.IsBetween(0, 3, BoundType.Closed)));
        }

        [Fact]
        public static void PeekRandomFromEmptyCollection()
        {
            False(Array.Empty<int>().PeekRandom(Random.Shared).HasValue);
        }

        [Fact]
        public static void PeekRandomFromSingletonCollection()
        {
            Equal(5, new int[] { 5 }.PeekRandom(Random.Shared));
        }

        [Fact]
        public static void PeekRandomFromCollection()
        {
            IReadOnlyCollection<int> collection = new int[] { 10, 20, 30 };
            All(Enumerable.Range(0, collection.Count), i =>
            {
                True(collection.PeekRandom(Random.Shared).Value is 10 or 20 or 30);
            });
        }
    }
}
