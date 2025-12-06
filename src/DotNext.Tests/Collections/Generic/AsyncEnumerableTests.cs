namespace DotNext.Collections.Generic;

public sealed class AsyncEnumerableTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task ForEachTestAsync(bool yieldIteration)
    {
        var list = new List<int> { 1, 10, 20 }.ToAsyncEnumerable(yieldIteration);
        var counter = new CollectionTests.Counter<int>();
        await list.ForEachAsync(counter.Accept, TestToken);
        Equal(3, counter.value);
        counter.value = 0;

        list = new[] { 1, 2, 10, 11, 15 }.ToAsyncEnumerable();
        await list.ForEachAsync(counter.Accept, TestToken);
        Equal(5, counter.value);
    }

    [Fact]
    public static async Task ForEachTest1Async()
    {
        var list = new List<int> { 1, 10, 20 }.ToAsyncEnumerable();
        var counter = new CollectionTests.Counter<int>();
        await list.ForEachAsync(counter.AcceptAsync, TestToken);
        Equal(3, counter.value);
        counter.value = 0;

        list = new[] { 1, 2, 10, 11, 15 }.ToAsyncEnumerable();
        await list.ForEachAsync(counter.AcceptAsync, TestToken);
        Equal(5, counter.value);
    }

    [Fact]
    public static async Task FirstOrNullTestAsync()
    {
        var array = Array.Empty<long>().ToAsyncEnumerable();
        var element = await array.FirstOrNullAsync(TestToken);
        Null(element);
        array = new long[] { 10, 20 }.ToAsyncEnumerable();
        element = await array.FirstOrNullAsync(TestToken);
        Equal(10, element);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task CopyListAsync(bool yieldIteration)
    {
        using var copy = await new List<int> { 10, 20, 30 }
            .ToAsyncEnumerable(yieldIteration)
            .CopyAsync(sizeHint: 4, token: TestToken);
        Equal(3, copy.Length);
        Equal(10, copy[0]);
        Equal(20, copy[1]);
        Equal(30, copy[2]);
    }

    [Fact]
    public static async Task CopyEmptyCollectionAsync()
    {
        using var copy = await Enumerable.Empty<int>().ToAsyncEnumerable().CopyAsync(token: TestToken);
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

        var array = await list.ToAsyncEnumerable().SkipNulls().ToArrayAsync(TestToken);
        Equal(2, array.Length);
        True(Array.Exists(array, "a".Equals));
        True(Array.Exists(array, "b".Equals));
    }

    [Fact]
    public static void Singleton()
    {
        var enumerable = IAsyncEnumerable<int>.Singleton(42);
        Equal(42, Single(enumerable));
    }
}