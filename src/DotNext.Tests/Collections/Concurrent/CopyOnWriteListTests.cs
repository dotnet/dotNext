namespace DotNext.Collections.Concurrent
{
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
            foreach (var item in list)
                list[0] = "empty";
        }

        [Fact]
        public static void AddRemove()
        {
            var list = new CopyOnWriteList<string>() { "one", "two" };
            Equal(2, list.Count);
            list.Add("three");
            Equal(3, list.Count);
            Equal("one", list[0]);
            Equal("two", list[1]);
            Equal("three", list[2]);
            Equal(2, list.RemoveAll(str => str.Length == 3));
            Equal(1, list.Count);
            Equal("three", list[0]);
            True(list.Remove("three"));
            Empty(list);
        }
    }
}
