namespace DotNext.Runtime;

public sealed class BoxedValueTests : Test
{
    [Fact]
    public static void BoxUnbox()
    {
        var obj = (BoxedValue<int>)42;
        Equal(42.GetHashCode(), obj.GetHashCode());
        Equal(42, obj.Unbox());
        Equal(42, obj);
        Equal(typeof(int), obj.GetType());
    }

    [Fact]
    public static void Unwrap()
    {
        object obj = null;
        Null(BoxedValue<int>.GetTypedReference(obj));

        obj = 42;
        Equal(42, BoxedValue<int>.GetTypedReference(obj));

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

        boxed1.Unbox().Value = 42;
        boxed2.Unbox().Value = 43;

        NotEqual(boxed1.Unbox().Value, boxed2.Unbox().Value);
    }
}