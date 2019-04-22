using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class SwitchTests: Assert
    {
        [Fact]
        public static void IntConversion()
        {
            var lambda = LambdaBuilder<Func<int, string>>.Build(fun =>
            {
                fun.Switch(fun.Parameters[0])
                    .Case(0, "Zero")
                    .Case(1, "One")
                    .Default("Unknown")
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
