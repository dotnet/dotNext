using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    using static CodeGenerator;
    using U = UniversalExpression;

    public sealed class SwitchTests: Assert
    {
        [Fact]
        public static void IntConversion()
        {
            var lambda = Lambda<Func<int, string>>(fun =>
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
            var lambda = LambdaBuilder<Func<string, int>>.Build(fun =>
            {
                fun.Switch(fun.Parameters[0])
                    .Case("Zero", 0)
                    .Case("One", 1)
                    .Default(int.MaxValue)
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
