using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Reflection;
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
        public void SimpleAsyncTest()
        {
            var lambda = LambdaBuilder<Func<int, int, Task<int>>>.Build(fun =>
            {
                UniversalExpression arg0 = fun.Parameters[0], arg1 = fun.Parameters[1];
                fun.Assign(fun.Result, new AsyncResultExpression(arg0 + arg1));
            })
            .Compile();
            Equal(42, lambda(40, 2).Result);
        }

        private static Task<long> Sum(long x, long y)
            => Task.FromResult(x + y);

        [Fact]
        public void AsyncTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = LambdaBuilder<Func<long, long, Task<long>>>.Build(fun =>
            {
                UniversalExpression arg0 = fun.Parameters[0], arg1 = fun.Parameters[1];
                var temp = fun.DeclareVariable<long>("tmp");
                fun.Assign(temp, new AwaitExpression(Expression.Call(null, sumMethod, arg0, arg1)));
                fun.AddStatement(new AsyncResultExpression(new UniversalExpression(temp) + 20L.AsConst()));
            });
            lambda = lambda.ToAsyncLambda();
            var fn = lambda.Compile();
            Equal(35L, fn(5L, 10L).Result);
        }
    }
}