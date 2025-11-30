namespace DotNext.Metaprogramming;

using Linq.Expressions;
using static CodeGenerator;

public sealed class SwitchTests : Test
{
    [Fact]
    public static void IntConversion()
    {
        var lambda = Lambda<Func<int, string>>(static fun =>
        {
            Switch(fun[0])
                .Case(0.Quoted, "Zero".Quoted)
                .Case(1.Quoted, "One".Quoted)
                .Default("Unknown".Quoted)
                .OfType<string>()
                .End();
        })
        .Compile();
        Equal("Zero", lambda(0));
        Equal("One", lambda(1));
        Equal("Unknown", lambda(3));
    }

    [Fact]
    public static void SwitchOverString()
    {
        var lambda = Lambda<Func<string, int>>(static fun =>
        {
            Switch(fun[0])
                .Case("Zero".Quoted, 0.Quoted)
                .Case("One".Quoted, 1.Quoted)
                .Default(int.MaxValue.Quoted)
                .OfType<int>()
                .End();
        })
        .Compile();
        Equal(0, lambda("Zero"));
        Equal(1, lambda("One"));
        Equal(int.MaxValue, lambda("Unknown"));
    }
}