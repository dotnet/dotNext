namespace DotNext.Runtime;

[TestClass]
public class BoxedValueTests
{
    [TestMethod]
    public void BoxUnbox()
    {
        var obj = (BoxedValue<int>)42;
        Assert.AreEqual(42.GetHashCode(), obj.GetHashCode());
        Assert.AreEqual(42, obj.Unbox());
        Assert.AreEqual(42, obj);
        Assert.AreEqual(typeof(int), obj.GetType());
    }

    [TestMethod]
    public void Unwrap()
    {
        object? obj = null;
        Assert.IsNull(BoxedValue<int>.GetTypedReference(obj));
    
        obj = 42;
        Assert.AreEqual(42, BoxedValue<int>.GetTypedReference(obj));
    
        obj = string.Empty;
        Assert.Throws<ArgumentException>(() => BoxedValue<int>.GetTypedReference(obj));
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
    
        boxed1.Unbox().Value = 42;
        boxed2.Unbox().Value = 43;
    
        Assert.AreNotEqual(boxed1.Unbox().Value, boxed2.Unbox().Value);
    }
}