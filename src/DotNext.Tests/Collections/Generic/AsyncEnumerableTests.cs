namespace DotNext.Collections.Generic;

public sealed class AsyncEnumerableTests : Test
{
    [Fact]
    public static async Task EmptyAsyncEnumerable()
    {
        var count = 0;

        await foreach (var item in AsyncEnumerable.Empty<int>())
            count++;

        Equal(0, count);
    }

    [Fact]
    public static async Task ElementAtIndexAsync()
    {
        var list = new LinkedList<long>();
        list.AddLast(10);
        list.AddLast(40);
        list.AddLast(100);

        var asyncList = list.ToAsyncEnumerable();
        Equal(100, await asyncList.ElementAtAsync(2));
        Equal(10, await asyncList.ElementAtAsync(0));
    }

    [Fact]
    public static async Task ForEachTestAsync()
    {
        var list = new List<int> { 1, 10, 20 }.ToAsyncEnumerable();
        var counter = new CollectionTests.Counter<int>();
        await list.ForEachAsync(counter.Accept);
        Equal(3, counter.value);
        counter.value = 0;

        list = new[] { 1, 2, 10, 11, 15 }.ToAsyncEnumerable();
        await list.ForEachAsync(counter.Accept);
        Equal(5, counter.value);
    }

    [Fact]
    public static async Task ForEachTest1Async()
    {
        var list = new List<int> { 1, 10, 20 }.ToAsyncEnumerable();
        var counter = new CollectionTests.Counter<int>();
        await list.ForEachAsync(counter.AcceptAsync);
        Equal(3, counter.value);
        counter.value = 0;

        list = new[] { 1, 2, 10, 11, 15 }.ToAsyncEnumerable();
        await list.ForEachAsync(counter.AcceptAsync);
        Equal(5, counter.value);
    }

    [Fact]
    public static async Task FirstOrNullTestAsync()
    {
        var array = new long[0].ToAsyncEnumerable();
        var element = await array.FirstOrNullAsync();
        Null(element);
        array = new long[] { 10, 20 }.ToAsyncEnumerable();
        element = await array.FirstOrNullAsync();
        Equal(10, element);
    }

    [Fact]
    public static async Task CopyListAsync()
    {
        using var copy = await new List<int> { 10, 20, 30 }.ToAsyncEnumerable().CopyAsync(sizeHint: 4);
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);
    }

    [Fact]
    public static async Task CopyEmptCollectionAsync()
    {
        using var copy = await Enumerable.Empty<int>().ToAsyncEnumerable().CopyAsync();
        True(copy.IsEmpty);
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
    public static void Singleton()
    {
        var enumerable = AsyncEnumerable.Singleton(42);
        Equal(42, Single(enumerable));
    }
}