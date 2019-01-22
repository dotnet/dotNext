using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class LambdaTests: Assert
    {   
        private static long Fact(long value)
        {
            return value > 1L ? value * Fact(value - 1) : value;
        }

        [Fact]
        public void RecursionTest()
        {
            var fact = LambdaBuilder<Func<long, long>>.Build(fun => 
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.If(arg > 1L).Then(arg * fun.Self.Invoke(arg - 1L)).Else(arg).OfType<long>().End();
            })
            .Compile();
            Equal(120, Fact(5));
            Equal(120, fact(5));
        }

        [Fact]
        public void AsyncTest()
        {
            var lambda = LambdaBuilder<Func<long, long, long>>.Build(fun =>
            {
                UniversalExpression arg0 = fun.Parameters[0], arg1 = fun.Parameters[1];
                fun.If(arg0 > arg1)
                    .Then(then =>
                    {
                        var localVar = fun.DeclareVariable<string>("local");
                        then.Assign(localVar, arg1.Call(nameof(long.ToString)));
                    })
                    .End();
                fun.Assign(fun.Result, arg0 + arg1);
            });
            lambda.ToAsyncLambda();
        }
    }
}