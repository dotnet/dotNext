namespace DotNext.Diagnostics.CodeAnalysis;

public sealed class MemoryOrderingAttributeTests : Test
{
    [Fact]
    public static void MemoryOrderingValues()
    {
        foreach (var ordering in Enum.GetValues<MemoryOrdering>())
        {
            Equal(ordering, new MemoryOrderingAttribute(ordering).Ordering);
        }
    }

    [Fact]
    public static void CheckDefaultAttribute()
    {
        True(new MemoryOrderingAttribute(MemoryOrdering.Unknown).IsDefaultAttribute());
        False(new MemoryOrderingAttribute(MemoryOrdering.Acquire).IsDefaultAttribute());
    }
}