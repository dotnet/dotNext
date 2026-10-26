using System.Runtime.CompilerServices;

namespace DotNext.Reflection;

public sealed class InlineArrayTypeTests : Test
{
    [Fact]
    public static void TypeCheck()
    {
        False(typeof(int).IsInlineArray);
        True(typeof(InlineArray2<int>).IsInlineArray);
    }

    [Fact]
    public static void InlineArrayTypeAndSize()
    {
        var (size, elementType) = typeof(int).InlineArrayTypeAndSize;
        Equal(0, size);
        Equal(typeof(void), elementType);

        (size, elementType) = typeof(InlineArray2<int>).InlineArrayTypeAndSize;
        Equal(2, size);
        Equal(typeof(int), elementType);
    }
}