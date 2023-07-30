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

    [Fact]
    public static void ToUntypedReference()
    {
        ValueType obj = BoxedValue<int>.Box(42);
        Equal(42, obj);
    }

    private struct MutableStruct
    {
        public int Value;
    }

    [Fact]
    public static void BitwiseCopyImmutable()
    {
        var boxed1 = (BoxedValue<int>)42;
        var boxed2 = boxed1.Copy();
        NotSame(boxed1, boxed2);
        Equal(42, boxed1);
        Equal(42, boxed2);
    }

    [Fact]
    public static void BitwiseCopyMutable()
    {
        var boxed1 = (BoxedValue<MutableStruct>)new MutableStruct();
        var boxed2 = boxed1.Copy();
        NotSame(boxed1, boxed2);

        boxed1.Value.Value = 42;
        boxed2.Value.Value = 43;

        NotEqual(boxed1.Value.Value, boxed2.Value.Value);
    }
}