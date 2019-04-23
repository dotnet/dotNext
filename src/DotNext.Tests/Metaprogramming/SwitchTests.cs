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
    }
}
