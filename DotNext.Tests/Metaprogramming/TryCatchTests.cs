using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class TryCatchTests: Assert
    {
        [Fact]
        public void DivisionByZeroTest()
        {
            var lambda = LambdaBuilder<Func<long, long, bool>>.Build(fun =>
            {
                ExpressionView param1 = fun.Parameters[0], param2 = fun.Parameters[1];
                fun.Try(param1 / param2)
                    .Fault(fault => fault.Return(fun, false))
                    .End();
                fun.Return(true);
            })
            .Compile();
            True(lambda(6, 3));
            False(lambda(6, 0));
        }
    }
}