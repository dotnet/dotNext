namespace DotNext;

public sealed class PredicateTests : Test
{
    [Fact]
    public static void PredefinedDelegatesTest()
    {
        Same(Predicate<string>.Constant(true), Predicate<string>.Constant(true));
        True(Predicate<string>.Constant(true).Invoke(""));
        False(Predicate<int>.Constant(false).Invoke(0));

        True(Predicate<string>.IsNull(null));
        False(Predicate<string>.IsNull(string.Empty));

        False(Predicate<string>.IsNotNull(null));
        True(Predicate<string>.IsNotNull(string.Empty));
    }

    [Fact]
    public static void NegateTest()
    {
        Predicate<string> predicate = Predicate<string>.IsNull;
        predicate = !predicate;

        False(predicate.Invoke(null));
        True(predicate.Invoke(string.Empty));
    }

    [Fact]
    public static void ConversionTest()
    {
        Predicate<string> predicate = static str => str.Length is 0;
        True(predicate.AsConverter().Invoke(""));

        predicate = static str => str.Length > 0;
        False(predicate.AsFunc().Invoke(""));
    }

    [Fact]
    public static void NullableHasValue()
    {
        Predicate<int?> pred = Predicate<int>.IsNotNull;
        True(pred(10));
        False(pred(null));
    }

    [Fact]
    public static void OrAndXor()
    {
        Predicate<int> pred1 = static i => i > 10;
        Predicate<int> pred2 = static i => i < 0;
        True((pred1 | pred2).Invoke(11));
        True((pred1 | pred2).Invoke(-1));
        False((pred1 | pred2).Invoke(8));

        pred2 = static i => i > 20;
        True((pred1 & pred2).Invoke(21));
        False((pred1 & pred2).Invoke(19));
        False((pred1 ^ pred2).Invoke(21));
        False((pred1 ^ pred2).Invoke(1));
        True((pred1 ^ pred2).Invoke(19));
    }

    [Fact]
    public static void TryInvoke()
    {
        Predicate<int> pred = static i => i > 10 ? true : throw new ArithmeticException();
        Equal(true, pred.TryInvoke(11));
        IsType<ArithmeticException>(pred.TryInvoke(9).Error);
    }

    [Fact]
    public static async Task ToAsync()
    {
        True(await Predicate<int>.Constant(true).ToAsync().Invoke(42, TestToken));
    }
}