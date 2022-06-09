using System.Diagnostics.CodeAnalysis;

namespace DotNext;

[ExcludeFromCodeCoverage]
public sealed class PredicateTests : Test
{
    [Fact]
    public static void PredefinedDelegatesTest()
    {
        True(Predicate.True<string>().Invoke(""));
        False(Predicate.False<int>().Invoke(0));
        True(Predicate.IsNull<string>().Invoke(null));
        False(Predicate.IsNull<string>().Invoke(""));
        False(Predicate.IsNotNull<string>().Invoke(null));
        True(Predicate.IsNotNull<string>().Invoke(""));
    }

    [Fact]
    public static void NegateTest()
    {
        False(Predicate.IsNull<string>().Negate().Invoke(null));
        True(Predicate.IsNull<string>().Negate().Invoke(""));
    }

    [Fact]
    public static void ConversionTest()
    {
        True(Predicate.AsConverter<string>(static str => str.Length == 0).Invoke(""));
        False(Predicate.AsFunc<string>(static str => str.Length > 0).Invoke(""));
    }

    [Fact]
    public static void NullableHasValue()
    {
        var pred = Predicate.HasValue<int>();
        True(pred(10));
        False(pred(null));
    }

    [Fact]
    public static void OrAndXor()
    {
        Predicate<int> pred1 = static i => i > 10;
        Predicate<int> pred2 = static i => i < 0;
        True(pred1.Or(pred2).Invoke(11));
        True(pred1.Or(pred2).Invoke(-1));
        False(pred1.Or(pred2).Invoke(8));

        pred2 = static i => i > 20;
        True(pred1.And(pred2).Invoke(21));
        False(pred1.And(pred2).Invoke(19));
        False(pred1.Xor(pred2).Invoke(21));
        False(pred1.Xor(pred2).Invoke(1));
        True(pred1.Xor(pred2).Invoke(19));
    }

    [Fact]
    public static void TryInvoke()
    {
        Predicate<int> pred = static i => i > 10 ? true : throw new ArithmeticException();
        Equal(true, pred.TryInvoke(11));
        IsType<ArithmeticException>(pred.TryInvoke(9).Error);
    }

    [Fact]
    public static void TypeCheck()
    {
        var obj = "Hello, world!";
        True(Predicate.IsTypeOf<string>().Invoke(obj));
        False(Predicate.IsTypeOf<int>().Invoke(obj));
    }
}