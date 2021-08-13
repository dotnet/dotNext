using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Collections.Generic
{
    [ExcludeFromCodeCoverage]
    public sealed class ListTests : Test
    {
        [Fact]
        public static void ToArray()
        {
            var list = new List<long>() { 10, 40, 100 };
            var array = list.ToArray(static i => i.ToString());
            True(array.SequenceEqual(new[] { "10", "40", "100" }));
        }

        private static int Compare(long x, long y) => x.CompareTo(y);

        [Fact]
        public static void OrderedInsertion()
        {
            Comparison<long> comparer = Compare;
            var list = new List<long> { 2L };
            list.InsertOrdered(1L, comparer);
            Equal(1L, list[0]);
            Equal(2L, list[1]);

            list = new List<long> { 1L };
            list.InsertOrdered(3L, comparer);
            Equal(1L, list[0]);
            Equal(3L, list[1]);

            list = new List<long> { 1L, 3L, 7L };
            Equal(2L, list.InsertOrdered(4L, comparer));
            list.RemoveRange(0, 0);
        }

        [Fact]
        public static void ReadOnlyView()
        {
            var view = new ReadOnlyListView<string, int>(new[] { "1", "2", "3" }, new Converter<string, int>(int.Parse));
            Equal(3, view.Count);
            Equal(1, view[0]);
            Equal(2, view[1]);
            Equal(3, view[2]);
            NotEmpty(view);
            foreach (var value in view)
                if (!value.IsBetween(0, 3, BoundType.Closed))
                    throw new Exception();
        }

        [Fact]
        public static void ReadOnlyIndexer()
        {
            IReadOnlyList<long> array = new[] { 5L, 6L, 20L };
            Equal(20L, List.Indexer<long>.ReadOnly(array, 2));
            Equal(6L, array.IndexerGetter().Invoke(1));
        }

        [Fact]
        public static void Indexer()
        {
            IList<long> array = new[] { 5L, 6L, 30L };
            Equal(30L, List.Indexer<long>.Getter(array, 2));
            List.Indexer<long>.Setter(array, 1, 10L);
            Equal(10L, array.IndexerGetter().Invoke(1));
            array.IndexerSetter().Invoke(0, 6L);
            Equal(6L, array.IndexerGetter().Invoke(0));
        }

        [Fact]
        public static void RemoveRange()
        {
            var list = new List<long> { 10L, 20L, 30L };
            list.RemoveRange(1..);
            NotEmpty(list);
            Equal(10L, list[0]);
        }

        private static void SliceTest(IList<long> list)
        {
            var slice = list.Slice(1..^1);
            NotEmpty(slice);
            Equal(2, slice.Count);
            Equal(20L, slice[0]);
            Equal(30L, slice[1]);
            Contains(30L, slice);
            Equal(1, slice.IndexOf(30L));
            Throws<ArgumentOutOfRangeException>(() => slice[2]);
            slice[0] = 50L;
            Equal(50L, list[1]);

            var index = 0;
            foreach (var item in slice)
                switch (index++)
                {
                    case 0:
                        Equal(50L, item);
                        continue;
                    case 1:
                        Equal(30L, item);
                        continue;
                    default:
                        throw new Xunit.Sdk.XunitException();
                }

            var array = new long[2];
            slice.CopyTo(array, 0);
            Equal(50L, array[0]);
            Equal(30L, array[1]);
        }

        [Fact]
        public static void SliceList()
        {
            SliceTest(new List<long> { 10L, 20L, 30L, 40L });
            SliceTest(new[] { 10L, 20L, 30L, 40L });
            SliceTest(new ArraySegment<long>(new[] { 10L, 20L, 30L, 40L }, 0, 4));
        }

        [Fact]
        public static void InsertRemove()
        {
            var list = new List<long> { 10L, 20L };
            list.Insert(^0, 30L);
            Equal(3, list.Count);
            Equal(30L, list[2]);
            list.RemoveAt(^1);
            Equal(2, list.Count);
        }

        [Fact]
        public static void ShuffleArray()
        {
            var array = new int[] { 1, 2, 3, 4, 5, 6, 7 };
            array.Shuffle(new Random());
            True(array[0] != 1 || array[1] != 2 || array[2] != 3 || array[3] != 4 || array[4] != 5);
        }

        [Fact]
        public static void ShuffleList()
        {
            var array = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            array.Shuffle(new Random());
            True(array[0] != 1 || array[1] != 2 || array[2] != 3 || array[3] != 4 || array[4] != 5);
        }

        [Fact]
        public static void ArraySlice()
        {
            var segment = List.Slice(new int[] { 10, 20, 30 }, 0..2);
            True(segment.TryGetSpan(out var span));
            Equal(2, span.Length);
            Equal(10, span[0]);
            Equal(20, span[1]);
        }

        [Fact]
        public static void EmptySegmentSlice()
        {
            var segment = default(ListSegment<int>);
            False(segment.TryGetSpan(out _));
        }

#if !NETCOREAPP3_1
        [Fact]
        public static void ListSlice()
        {
            var segment = List.Slice(new List<int> { 10, 20, 30 }, 0..2);
            True(segment.TryGetSpan(out var span));
            Equal(2, span.Length);
            Equal(10, span[0]);
            Equal(20, span[1]);
        }
#endif
    }
}
