using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

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
        All(view, static value => True(value is >= 0 and <= 3));
    }

    internal sealed class Counter<T>
    {
        public int value;

        public void Accept(T item) => value += 1;

        public ValueTask AcceptAsync(T item, CancellationToken token)
        {
            ValueTask task = ValueTask.CompletedTask;
            try
            {
                Accept(item);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }

            return task;
        }
    }

    [Fact]
    public static void ForEachTest()
    {
        IList<int> list = new List<int> { 1, 10, 20 };
        var counter = new Counter<int>();
        list.ForEach(counter.Accept);
        Equal(3, counter.value);
        counter.value = 0;
        var array2 = new int[] { 1, 2, 10, 11, 15 };
        array2.ForEach(counter.Accept);
        Equal(5, counter.value);
    }
    
    [Fact]
    public static async Task ForEachTestAsync()
    {
        IList<int> list = new List<int> { 1, 10, 20 };
        var counter = new Counter<int>();
        await list.ForEachAsync(counter.AcceptAsync);
        Equal(3, counter.value);
        counter.value = 0;
        
        var array2 = new int[] { 1, 2, 10, 11, 15 };
        await array2.ForEachAsync(counter.AcceptAsync);
        Equal(5, counter.value);
    }

    [Fact]
    public static void ElementAtIndex()
    {
        var list = new LinkedList<long>();
        list.AddLast(10);
        list.AddLast(40);
        list.AddLast(100);
        list.ElementAt(2, out var element);
        Equal(100, element);
        list.ElementAt(0, out element);
        Equal(10, element);
    }

    [Fact]
    public static void SkipNullsTest()
    {
        var list = new LinkedList<string>();
        list.AddLast("a");
        list.AddLast(default(string));
        list.AddLast("b");
        list.AddLast(default(string));
        Equal(4, list.Count);
        var array = list.SkipNulls().ToArray();
        Equal(2, array.Length);
        True(Array.Exists(array, "a".Equals));
        True(Array.Exists(array, "b".Equals));
    }

    [Fact]
    public static void ToStringTest()
    {
        var array = new int[] { 10, 20, 30 };
        var str = array.ToString(":");
        Equal("10:20:30", str);
    }

    [Fact]
    public static void Prepend()
    {
        IEnumerable<string> items = new[] { "One", "Two" };
        items = items.Prepend("Zero");
        NotEmpty(items);
        Equal(3, items.Count());
        Equal("Zero", items.First());
        Equal("Two", items.Last());
    }

    [Fact]
    public static void Append()
    {
        IEnumerable<string> items = new[] { "One", "Two" };
        items = items.Append("Three", "Four");
        NotEmpty(items);
        Equal(4, items.Count());
        Equal("One", items.First());
        Equal("Four", items.Last());
    }

    [Fact]
    public static async Task IterationAsync()
    {
        var collection = Array.Empty<int>().ToAsyncEnumerable();
        Null(await collection.FirstOrNullAsync());
        Equal(Optional<int>.None, await collection.FirstOrNoneAsync());
        Equal(Optional<int>.None, await collection.FirstOrNoneAsync(Predicate.Constant<int>(true)));
        collection = new int[] { 42 }.ToAsyncEnumerable();
        Equal(42, await collection.FirstOrNullAsync());
        Equal(42, await collection.FirstOrNoneAsync());
        Equal(42, await collection.FirstOrNoneAsync(Predicate.Constant<int>(true)));
    }

    [Fact]
    public static async Task Iteration2Async()
    {
        var collection = Array.Empty<int>().ToAsyncEnumerable();
        Null(await collection.LastOrNullAsync());
        Equal(Optional<int>.None, await collection.LastOrNoneAsync());
        collection = new int[] { 42 }.ToAsyncEnumerable();
        Equal(42, await collection.LastOrNullAsync());
        Equal(42, await collection.LastOrNoneAsync());
    }

    [Fact]
    public static async Task ConversionToAsyncEnumerable()
    {
        int index = 0;
        await foreach (var item in new int[] { 10, 20, 30 }.ToAsyncEnumerable())
        {
            switch (index++)
            {
                case 0:
                    Equal(10, item);
                    break;
                case 1:
                    Equal(20, item);
                    break;
                case 2:
                    Equal(30, item);
                    break;
                default:
                    Fail($"Unexpected element {item}");
                    break;
            }
        }
    }

    [Fact]
    public static void EmptyConsumingEnumerable()
    {
        var enumerable = new Collection.ConsumingEnumerable<int>();
        Empty(enumerable);
    }

    [Fact]
    public static void ConsumeQueue()
    {
        var queue = new ConcurrentQueue<int>();
        queue.Enqueue(42);
        queue.Enqueue(52);

        var consumer = queue.GetConsumer();
        Collection(consumer, static i => Equal(42, i), static i => Equal(52, i));
    }

    [Fact]
    public static void ConsumeStack()
    {
        var queue = new ConcurrentStack<int>();
        queue.Push(42);
        queue.Push(52);

        var consumer = queue.GetConsumer();
        Collection(consumer, static i => Equal(52, i), static i => Equal(42, i));
    }

    [Fact]
    public static void CopyArray()
    {
        using var copy = new int[] { 10, 20, 30 }.Copy();
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);
    }

    [Fact]
    public static void CopyList()
    {
        using var copy = new List<int> { 10, 20, 30 }.Copy();
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);
    }

    [Fact]
    public static void CopyLinkedList()
    {
        using var copy = new LinkedList<int>(new int[] { 10, 20, 30 }).Copy();
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);
    }

    [Fact]
    public static void CopyEnumerable()
    {
        using var copy = GetElements().Copy();
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);

        static IEnumerable<int> GetElements()
        {
            yield return 10;
            yield return 20;
            yield return 30;
        }
    }

    [Fact]
    public static void CopyEmptyCollection()
    {
        using var copy = Enumerable.Empty<int>().Copy();
        True(copy.IsEmpty);
    }

    [Fact]
    public static void CopyString()
    {
        using var copy = "abcd".Copy();
        Equal("abcd", copy.Memory.ToString());
    }

    [Fact]
    public static void FirstOrNone()
    {
        Equal(5, new[] { 5, 6 }.FirstOrNone());
        Equal(5, new List<int> { 5, 6 }.FirstOrNone());
        Equal(5, new LinkedList<int>([5, 6]).FirstOrNone());
        Equal('5', "56".FirstOrNone());
        Equal(5, ImmutableArray.Create([5, 6]).FirstOrNone());
        Equal(5, GetValues().FirstOrNone());

        Equal(Optional<int>.None, Array.Empty<int>().FirstOrNone());
        Equal(Optional<int>.None, new List<int>().FirstOrNone());
        Equal(Optional<int>.None, new LinkedList<int>().FirstOrNone());
        Equal(Optional<char>.None, string.Empty.FirstOrNone());
        Equal(Optional<int>.None, ImmutableArray<int>.Empty.FirstOrNone());
        Equal(Optional<int>.None, EmptyEnumerable<int>().FirstOrNone());

        static IEnumerable<int> GetValues()
        {
            yield return 5;
            yield return 6;
        }
    }
    
    [Fact]
    public static void LastOrNone()
    {
        Equal(6, new[] { 5, 6 }.LastOrNone());
        Equal(6, new List<int> { 5, 6 }.LastOrNone());
        Equal(6, new LinkedList<int>([5, 6]).LastOrNone());
        Equal('6', "56".LastOrNone());
        Equal(6, ImmutableArray.Create([5, 6]).LastOrNone());
        Equal(6, GetValues().LastOrNone());

        Equal(Optional<int>.None, Array.Empty<int>().LastOrNone());
        Equal(Optional<int>.None, new List<int>().LastOrNone());
        Equal(Optional<int>.None, new LinkedList<int>().LastOrNone());
        Equal(Optional<char>.None, string.Empty.LastOrNone());
        Equal(Optional<int>.None, ImmutableArray<int>.Empty.LastOrNone());
        Equal(Optional<int>.None, EmptyEnumerable<int>().LastOrNone());

        static IEnumerable<int> GetValues()
        {
            yield return 5;
            yield return 6;
        }
    }

    static IEnumerable<T> EmptyEnumerable<T>()
    {
        yield break;
    }
}