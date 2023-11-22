using System.Buffers;
using System.Collections.Concurrent;

namespace DotNext.Collections.Generic;

public sealed class SequenceTests : Test
{
    internal sealed class Counter<T>
    {
        public int value;

        public void Accept(T item) => value += 1;
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
    public static async Task SkipNullsTestAsync()
    {
        var list = new LinkedList<string>();
        list.AddLast("a");
        list.AddLast(default(string));
        list.AddLast("b");
        list.AddLast(default(string));
        Equal(4, list.Count);

        var array = await list.ToAsyncEnumerable().SkipNulls().ToArrayAsync();
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
}