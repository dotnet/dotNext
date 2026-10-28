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
}