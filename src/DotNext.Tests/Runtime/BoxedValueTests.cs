namespace DotNext.Runtime;

public sealed class BoxedValueTests : Test
{
    [Fact]
    public static void BoxUnbox()
    {
        var obj = (BoxedValue<int>)42;
        Equal(42.GetHashCode(), obj.GetHashCode());
        Equal(42, obj.Value);
        Equal(42, (int)obj);
        Equal(typeof(int), obj.GetType());
    }

    [Fact]
    public static void Unwrap()
    {
        object obj = null;
        Null(BoxedValue<int>.GetTypedReference(obj));

        obj = 42;
        Equal(42, BoxedValue<int>.GetTypedReference(obj).Value);

        obj = string.Empty;
        Throws<ArgumentException>(() => BoxedValue<int>.GetTypedReference(obj));
    }
}