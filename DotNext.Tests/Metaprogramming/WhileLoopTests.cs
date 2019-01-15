using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class WhileLoopTests: Assert
    {
        [Fact]
        public void FactorialTest()
        {
            var factorial = LambdaBuilder<Func<long, long>>.Build(fun => 
            {
                ExpressionView input = fun.Parameters[0];
                ExpressionView result = fun.DeclareVariable("result", 1L);
                fun.While(input > 1L, loop =>
                {
                    loop.AssignStatement(result, result * input);
                    loop.AssignStatement(input, input - 1L);
                });
                fun.Return(result);
            })
            .Compile();
            Equal(6, factorial(3));
        }
    }
}