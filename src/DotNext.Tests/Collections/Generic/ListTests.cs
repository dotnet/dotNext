namespace DotNext.Collections.Generic
{
    public sealed class ListTests : Assert
    {
        [Fact]
        public static void ToArray()
        {
            var list = new List<long>() { 10, 40, 100 };
            var array = list.ToArray(i => i.ToString());
            True(array.SequenceEqual(new[] { "10", "40", "100" }));
        }

        private static int Compare(long x, long y) => x.CompareTo(y);

        [Fact]
        public static void OrderedInsertion()
        {
            var comparer = new ValueFunc<long, long, int>(Compare);
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
    }
}
