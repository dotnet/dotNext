namespace DotNext.Runtime;

[TestClass]
public sealed class BoxedValueTests
{
    [TestMethod]
    public void BoxUnbox()
    {
        var obj = (BoxedValue<int>)42;
        Assert.AreEqual(42.GetHashCode(), obj.GetHashCode());
        Assert.AreEqual(42, obj.Value);
        Assert.AreEqual(42, (int)obj);
        Assert.AreEqual(typeof(int), obj.GetType());
    }

    [TestMethod]
    public void Unwrap()
    {
        object? obj = null;
        Assert.IsNull(BoxedValue<int>.GetTypedReference(obj));

        obj = 42;
        Assert.AreEqual(42, BoxedValue<int>.GetTypedReference(obj).Value);

        obj = string.Empty;
        Assert.ThrowsException<ArgumentException>(() => BoxedValue<int>.GetTypedReference(obj));
    }

    [TestMethod]
    public void ToUntypedReference()
    {
        ValueType obj = BoxedValue<int>.Box(42);
        Assert.AreEqual(42, obj);
    }

    private struct MutableStruct
    {
        public int Value;
    }

    [TestMethod]
    public void BitwiseCopyImmutable()
    {
        var boxed1 = (BoxedValue<int>)42;
        var boxed2 = boxed1.Copy();
        Assert.AreNotSame(boxed1, boxed2);
        Assert.AreEqual(42, boxed1);
        Assert.AreEqual(42, boxed2);
    }

    [TestMethod]
    public void BitwiseCopyMutable()
    {
        var boxed1 = (BoxedValue<MutableStruct>)new MutableStruct();
        var boxed2 = boxed1.Copy();
        Assert.AreNotSame(boxed1, boxed2);

        boxed1.Value.Value = 42;
        boxed2.Value.Value = 43;

        Assert.AreNotEqual(boxed1.Value.Value, boxed2.Value.Value);
    }
}