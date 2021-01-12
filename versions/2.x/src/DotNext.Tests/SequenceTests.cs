using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    [Obsolete("This is a set of tests for obsolete Sequence class")]
    public sealed class SequenceTests : Test
    {
        private sealed class Counter<T>
        {
            public int value;

            public void Accept(T item) => value += 1;
        }

        [Fact]
        public static void ForEachTest()
        {
            IList<int> list = new List<int> { 1, 10, 20 };
            var counter = new Counter<int>();
            Sequence.ForEach(list, counter.Accept);
            Equal(3, counter.value);
            counter.value = 0;
            var array2 = new int[] { 1, 2, 10, 11, 15 };
            Sequence.ForEach(array2, counter.Accept);
            Equal(5, counter.value);
        }

        [Fact]
        public static async Task ForEachTestAsync()
        {
            var list = Sequence.ToAsyncEnumerable(new List<int> { 1, 10, 20 });
            var counter = new Counter<int>();
            await Sequence.ForEachAsync(list, counter.Accept);
            Equal(3, counter.value);
            counter.value = 0;
            
            list = Sequence.ToAsyncEnumerable(new int[] { 1, 2, 10, 11, 15 });
            await Sequence.ForEachAsync(list, counter.Accept);
            Equal(5, counter.value);
        }

        [Fact]
        public static void FirstOrNullTest()
        {
            var array = new long[0];
            var element = Sequence.FirstOrNull(array);
            Null(element);
            array = new long[] { 10, 20 };
            element = Sequence.FirstOrNull(array);
            Equal(10, element);
        }

        [Fact]
        public static async Task FirstOrNullTestAsync()
        {
            var array = Sequence.ToAsyncEnumerable(new long[0]);
            var element = await Sequence.FirstOrNullAsync(array);
            Null(element);
            array = Sequence.ToAsyncEnumerable(new long[] { 10, 20 });
            element = await Sequence.FirstOrNullAsync(array);
            Equal(10, element);
        }

        [Fact]
        public static void ElementAtIndex()
        {
            var list = new LinkedList<long>();
            list.AddLast(10);
            list.AddLast(40);
            list.AddLast(100);
            Sequence.ElementAt(list, 2, out var element);
            Equal(100, element);
            Sequence.ElementAt(list, 0, out element);
            Equal(10, element);
        }

        [Fact]
        public static async Task ElementAtIndexAsync()
        {
            var list = new LinkedList<long>();
            list.AddLast(10);
            list.AddLast(40);
            list.AddLast(100);

            var asyncList = Sequence.ToAsyncEnumerable(list);
            Equal(100, await Sequence.ElementAtAsync(asyncList, 2));
            Equal(10, await Sequence.ElementAtAsync(asyncList, 0));
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
            var array = Sequence.SkipNulls(list).ToArray();
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

            var array = await Sequence.ToArrayAsync(Sequence.SkipNulls(Sequence.ToAsyncEnumerable(list)));
            Equal(2, array.Length);
            True(Array.Exists(array, "a".Equals));
            True(Array.Exists(array, "b".Equals));
        }

        [Fact]
        public static void ToStringTest()
        {
            var array = new int[] { 10, 20, 30 };
            var str = Sequence.ToString(array, ":");
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
            items = Sequence.Append(items, "Three", "Four");
            NotEmpty(items);
            Equal(4, items.Count());
            Equal("One", items.First());
            Equal("Four", items.Last());
        }

        [Fact]
        public static void Skip()
        {
            var range = Enumerable.Range(0, 10);
            using var enumerator = range.GetEnumerator();
            True(Sequence.Skip(enumerator, 8));
            True(enumerator.MoveNext());
            Equal(8, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(9, enumerator.Current);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static async Task SkipAsync()
        {
            var range = Enumerable.Range(0, 10);
            await using var enumerator = Sequence.GetAsyncEnumerator(range);
            True(await Sequence.SkipAsync(enumerator, 8));
            True(await enumerator.MoveNextAsync());
            Equal(8, enumerator.Current);
            True(await enumerator.MoveNextAsync());
            Equal(9, enumerator.Current);
            False(await enumerator.MoveNextAsync());
        }

        [Fact]
        public static void SkipValueEnumerator()
        {
            var list = new List<long> { 10L, 20L, 30L };
            var enumerator = list.GetEnumerator();
            True(Sequence.Skip<List<long>.Enumerator, long>(ref enumerator, 2));
            True(enumerator.MoveNext());
            Equal(30L, enumerator.Current);
            enumerator.Dispose();
        }

        [Fact]
        public static void LimitedSequence()
        {
            var range = Enumerable.Range(0, 10);
            using var enumerator = Sequence.Limit(range.GetEnumerator(), 3);
            True(enumerator.MoveNext());
            Equal(0, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(1, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(2, enumerator.Current);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static void Iteration()
        {
            IEnumerable<int> collection = Array.Empty<int>();
            Null(Sequence.FirstOrNull(collection));
            Equal(Optional<int>.None, Sequence.FirstOrEmpty(collection));
            Equal(Optional<int>.None, Sequence.FirstOrEmpty(collection, Predicate.True<int>()));
            collection = new int[] { 42 };
            Equal(42, Sequence.FirstOrNull(collection));
            Equal(42, Sequence.FirstOrEmpty(collection));
            Equal(42, Sequence.FirstOrEmpty(collection, Predicate.True<int>()));
        }

        [Fact]
        public static async Task IterationAsync()
        {
            var collection = Sequence.ToAsyncEnumerable(Array.Empty<int>());
            Null(await Sequence.FirstOrNullAsync(collection));
            Equal(Optional<int>.None, await Sequence.FirstOrEmptyAsync(collection));
            Equal(Optional<int>.None, await Sequence.FirstOrEmptyAsync(collection, Predicate.True<int>()));
            collection = Sequence.ToAsyncEnumerable(new int[] { 42 });
            Equal(42, await Sequence.FirstOrNullAsync(collection));
            Equal(42, await Sequence.FirstOrEmptyAsync(collection));
            Equal(42, await Sequence.FirstOrEmptyAsync(collection, Predicate.True<int>()));
        }

        [Fact]
        public static async Task ConversionToAsyncEnumerable()
        {
            int index = 0;
            await foreach(var item in Sequence.ToAsyncEnumerable(new int[] { 10, 20, 30}))
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
                        throw new Xunit.Sdk.XunitException();
                }
            }
        }

        [Fact]
        public static async Task ConversionToAsyncEnumerator()
        {
            await using var enumerator = Sequence.GetAsyncEnumerator(new int[] { 10, 20, 30});
            for (int index = 0; await enumerator.MoveNextAsync(); index++)
            {
                switch (index)
                {
                    case 0:
                        Equal(10, enumerator.Current);
                        break;
                    case 1:
                        Equal(20, enumerator.Current);
                        break;
                    case 2:
                        Equal(30, enumerator.Current);
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException();
                }
            }
        }

        [Fact]
        public static async Task CanceledAsyncEnumerator()
        {
            await using var enumerator = Sequence.GetAsyncEnumerator(new int[] { 10, 20, 30}, new CancellationToken(true));
            await ThrowsAsync<TaskCanceledException>(enumerator.MoveNextAsync().AsTask);
        }
    }
}
