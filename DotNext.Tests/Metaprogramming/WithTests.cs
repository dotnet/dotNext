using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class WithTests: Assert
    {
        [Fact]
        public void WithBlockTest()
        {
            var lambda = LambdaBuilder<Func<int, int>>.Build(fun =>
            {
                ExpressionView arg = fun.Parameters[0];
                fun.With(arg + 10, scope => scope.Assign(scope.ScopeVar, scope.ScopeVar * 2));
            })
            .Compile();
            Equal(28, lambda(4));
        }
    }
}