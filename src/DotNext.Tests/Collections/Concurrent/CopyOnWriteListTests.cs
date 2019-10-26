using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Collections.Concurrent
{
    [ExcludeFromCodeCoverage]
    public sealed class CopyOnWriteListTests : Assert
    {
        [Fact]
        public static void Enumeration()
        {
            var list = new CopyOnWriteList<string>(new[] { "one", "two" });
            Equal(2, list.Count);
            Equal("one", list[0]);
            Equal("two", list[1]);
            //checks whether the enumeration doesn't throw exception if item is changed
            foreach (ref readonly var item in list)
                list[0] = "empty";
            using (IEnumerator<string> enumerator = list.GetEnumerator())
            {
                True(enumerator.MoveNext());
                Equal("empty", enumerator.Current);
                True(enumerator.MoveNext());
                Equal("two", enumerator.Current);
                False(enumerator.MoveNext());
                enumerator.Reset();
                True(enumerator.MoveNext());
                Equal("empty", enumerator.Current);
                True(enumerator.MoveNext());
                Equal("two", enumerator.Current);
                False(enumerator.MoveNext());
            }
        }

        [Fact]
        public static void AddRemove()
        {
            var list = new CopyOnWriteList<string>() { "one", "two" };
            Equal(2, list.Count);
            list.Add("three");
            Contains("two", list);
            DoesNotContain("four", list);
            Equal(3, list.Count);
            Equal("one", list[0]);
            Equal("two", list[1]);
            Equal("three", list[2]);
            Equal(2, list.RemoveAll(str => str.Length == 3));
            Equal(1, list.Count);
            Equal("three", list[0]);
            True(list.Remove("three"));
            Empty(list);
            list.Add("four");
            list.Clear();
            Empty(list);
        }

        [Fact]
        public static void SearchOperations()
        {
            var list = new CopyOnWriteList<string>() { "one", "two", "three", "one" };
            Contains("two", list);
            DoesNotContain("four", list);
            True(list.Exists("two".Equals));
            False(list.Exists("four".Equals));
            NotNull(list.Find("one".Equals));
            Null(list.Find("four".Equals));
            Equal(0, list.FindIndex("one".Equals));
            Equal(3, list.FindLastIndex("one".Equals));
            Equal(1, list.IndexOf("two"));
            Equal(0, list.IndexOf("one"));
            Equal(3, list.LastIndexOf("one"));
        }

        [Fact]
        public static void CopyElements()
        {
            var list = new CopyOnWriteList<string>() { "one", "two", "three" };
            var array = new string[3];
            list.CopyTo(array, 0);
            Equal("one", array[0]);
            Equal("two", array[1]);
            Equal("three", array[2]);
        }

        [Fact]
        public static void ReadOnlySnapshot()
        {
            var list = new CopyOnWriteList<int>() { 1, 2 };
            var view = list.Snapshot;
            list[1] = 10;
            Equal(1, list[0]);
            Equal(10, list[1]);
            Equal(1, view.Span[0]);
            Equal(2, view.Span[1]);
        }
    }
}
