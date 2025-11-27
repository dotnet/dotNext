namespace DotNext.Runtime;

public sealed class VariantTests : Test
{
    [Fact]
    public static void EmptyValue()
    {
        True(Variant.Empty.IsEmpty);
        False(Variant.Empty.IsMutable);
        True(Variant.Empty == default);
        False(Variant.Empty != default);
        
        Same(typeof(void), Variant.Empty.TargetType);
    }

    [Fact]
    public static void BoxedValue()
    {
        ValueType value = 42;
        var variant = Variant.Mutable(value);
        True(variant.IsMutable);
        Same(typeof(int), variant.TargetType);
        
        variant.Mutable<int>() = 43;
        Equal(43, value);
    }

    [Fact]
    public static void ToObject()
    {
        Null(Variant.Empty.ToObject());

        var i = 42;
        Equal(42, Variant.Immutable(in i).ToObject());

        var str = "Hello, world!";
        Same(str, Variant.Immutable(in str).ToObject());
    }

    [Fact]
    public static void Immutability()
    {
        var i = 42;
        False(Variant.Immutable(in i).IsMutable);
        True(Variant.Mutable(ref i).IsMutable);
    }

    [Fact]
    public static void Equality()
    {
        var i = 42;
        var x = Variant.Immutable(in i);
        var y = Variant.Immutable(in i);
        
        True(x == y);
    }
}