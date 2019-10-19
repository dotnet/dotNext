namespace DotNext.Reflection
{
    public sealed class CollectionTypeTests : Assert
    {
        [Fact]
        public static void GetItemTypeTest()
        {
            Equal(typeof(long), typeof(long[]).GetItemType());
            Equal(typeof(bool), typeof(IList<bool>).GetItemType());
        }
    }
}
