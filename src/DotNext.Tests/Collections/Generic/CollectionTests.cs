namespace DotNext.Collections.Generic;

public sealed class CollectionTests : Test
{
    [Fact]
    public static void AddingItemsToList()
    {
        var expected = new HashSet<int>(new[] { 1, 3, 5 });
        AddItems<List<int>>(expected);
        AddItems<HashSet<int>>(expected);
        AddItems<LinkedList<int>>(expected);

        static void AddItems<TCollection>(IReadOnlySet<int> expected)
            where TCollection : class, ICollection<int>, new()
        {
            var actual = new TCollection();
            actual.AddAll(expected);
            True(expected.SetEquals(actual));
        }
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
}