using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;

    [ExcludeFromCodeCoverage]
    public sealed class SwitchTests : Test
    {
        [Fact]
        public static void IntConversion()
        {
            var lambda = Lambda<Func<int, string>>(static fun =>
            {
                Switch(fun[0])
                    .Case(0.Const(), "Zero".Const())
                    .Case(1.Const(), "One".Const())
                    .Default("Unknown".Const())
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
                    .Case("Zero".Const(), 0.Const())
                    .Case("One".Const(), 1.Const())
                    .Default(int.MaxValue.Const())
                    .OfType<int>()
                    .End();
            })
            .Compile();
            Equal(0, lambda("Zero"));
            Equal(1, lambda("One"));
            Equal(int.MaxValue, lambda("Unknown"));
        }
    }
}
