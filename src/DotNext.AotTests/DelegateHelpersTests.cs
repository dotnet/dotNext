namespace DotNext;

public class DelegateHelpersTests
{
    [TestMethod]
    public void ConstantProvider()
    {
        Assert.AreSame(Func.Constant<string?>(null), Func.Constant<string?>(null));
        Assert.IsNull(Func.Constant<string?>(null).Invoke());

        Assert.AreSame(Func.Constant(true), Func.Constant(true));
        Assert.AreSame(Func.Constant(false), Func.Constant(false));

        Assert.AreEqual(42, Func.Constant(42).Invoke());
        Assert.AreEqual("Hello, world", Func.Constant("Hello, world").Invoke());
    }

    [TestMethod]
    public void ValueTypeConst()
    {
        const long value = 42L;
        var provider = Func.Constant(value);
        Assert.AreEqual(value, provider.Target);
    }

    [TestMethod]
    public void StringConst()
    {
        const string value = "Hello, world";
        var provider = Func.Constant(value);
        Assert.AreSame(value, provider.Target);
    }
}