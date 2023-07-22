using System.Collections;

namespace DotNext.Reflection;

public sealed class CollectionTypeTests : Test
{
    [Fact]
    public static void GetItemTypeTest()
    {
        Equal(typeof(long), typeof(long[]).GetItemType());
        Equal(typeof(bool), typeof(IList<bool>).GetItemType());
        Equal(typeof(object), typeof(IEnumerable).GetItemType());
        Equal(typeof(int), typeof(IAsyncEnumerable<int>).GetItemType());
    }
}