using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class SwitchTests: Assert
    {
        [Fact]
        public void IntConversionTest()
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
    }
}
