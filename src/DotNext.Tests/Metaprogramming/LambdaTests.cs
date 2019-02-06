using System;
using System.IO;
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
        public void AsyncLambdaWithoutAwaitTest()
        {
            var lambda = LambdaBuilder<Func<int, int, Task<int>>>.Build(fun =>
            {
                UniversalExpression arg0 = fun.Parameters[0], arg1 = fun.Parameters[1];
                fun.Assign(fun.Result, new AsyncResultExpression(arg0 + arg1));
            });
            Equal(42, lambda.Compile().Invoke(40, 2).Result);
        }

        private static Task<long> Sum(long x, long y)
            => Task.FromResult(x + y);

        [Fact]
        public void SimpleAsyncLambdaTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambdaBuilder<Func<long, long, Task<long>>>.Build(fun =>
            {
                UniversalExpression arg0 = fun.Parameters[0], arg1 = fun.Parameters[1];
                UniversalExpression temp = fun.DeclareVariable<long>("tmp");
                fun.Assign(temp, Expression.Call(null, sumMethod, arg0, arg1).Await());
                fun.Return(temp + 20L.AsConst());
            });
            var fn = lambda.Compile();
            Equal(35L, fn(5L, 10L).Result);
        }

        [Fact]
        public void AsyncLambdaWithConditionalTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambdaBuilder<Func<long, Task<long>>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.If(arg > 10L)
                    .Then(then => then.Return(Expression.Call(null, sumMethod, arg, 10L.AsConst()).Await()))
                    .Else(@else =>
                    {
                        var local = @else.DeclareVariable<long>("myVar");
                        @else.Assign(local, Expression.Call(null, sumMethod, arg, 90L.AsConst()).Await());
                        @else.Return(local);
                    })
                    .End();
            });
            var fn = lambda.Compile();
            Equal(25L, fn(15L).Result);
            Equal(99L, fn(9L).Result);
        }

        [Fact]
        public void TryFinallyAsyncTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambdaBuilder<Func<long[], Task<long>>>.Build(fun =>
            {
                var result = fun.DeclareVariable<long>("accumulator");
                fun.ForEach(fun.Parameters[0], loop =>
                {
                    loop.If(loop.Element == 0L).Then(then => then.Break(loop)).End();
                    loop.Assign(result, Expression.Call(null, sumMethod, result, loop.Element).Await());
                });
                fun.Return(result);
            });
            var fn = lambda.Compile();
            Equal(15L, fn(new[] { 3L, 2L, 10L }).Result);
            Equal(5, fn(new[] { 3L, 2L, 0L, 10L }).Result);
        }

        [Fact]
        public void TryCatchAsyncLambdaTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambdaBuilder<Func<long, Task<long>>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.Try(block =>
                {
                    block.If(arg < 0L).Then(then => then.Throw<InvalidOperationException>()).End();
                    block.If(arg > 10L).Then(then => then.Throw<ArgumentException>()).Else(@else => @else.Return(arg)).End();
                })
                .Catch<ArgumentException>(@catch => @catch.Return(-42L))
                .Catch<InvalidOperationException>(@catch => @catch.Rethrow())
                .End();
            });
            var fn = lambda.Compile();
            Equal(5L, fn(5L).Result);
            Equal(-42L, fn(80L).Result);
            var exception = Throws<AggregateException>(() => fn(-10L).Result);
            IsType<InvalidOperationException>(exception.InnerException);
        }

        [Fact]
        public void LeaveAsyncTryCatchTest()
        {
            var lambda = AsyncLambdaBuilder<Func<long[], Task<string>>>.Build(body =>
            {
                var array = body.Parameters[0];
                body.For(0, i => i < array.ArrayLength(), loop =>
                {
                    loop.Using(typeof(MemoryStream).New(), @using =>
                    {
                        @using.Break(loop);
                    });
                });
            });
            var fn = lambda.Compile();
            Null(fn(new[] { 1L }).Result);
        }
    }
}