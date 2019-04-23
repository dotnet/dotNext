using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Reflection;
using Xunit;

namespace DotNext.Metaprogramming
{
    using U = UniversalExpression;
    using static CodeGenerator;

    public sealed class LambdaTests: Assert
    {   
        private static long Fact(long value)
        {
            return value > 1L ? value * Fact(value - 1) : value;
        }

        [Fact]
        public static void Recursion()
        {
            var fact = Lambda<Func<long, long>>(fun => 
            {
                var arg = (U)fun[0];
                If(arg > 1L).Then(arg * fun.Invoke(arg - 1L)).Else(arg).OfType<long>().End();
            })
            .Compile();
            Equal(120, Fact(5));
            Equal(120, fact(5));
        }

        [Fact]
        public static void AsyncLambdaWithoutAwait()
        {
            var lambda = Lambda<Func<int, int, Task<int>>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Assign(result, new AsyncResultExpression((U)arg1 + arg2, false));
            });
            Equal(42, lambda.Compile().Invoke(40, 2).Result);
        }

        [Fact]
        public static void AsyncLambdaWithoutAwaitValueTask()
        {
            var lambda = Lambda<Func<int, int, ValueTask<int>>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Assign(result, new AsyncResultExpression((U)arg1 + arg2, true));
            });
            Equal(42, lambda.Compile().Invoke(40, 2).Result);
        }

        private static Task<long> Sum(long x, long y)
            => Task.FromResult(x + y);

        [Fact]
        public static void SimpleAsyncLambda()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambda<Func<long, long, Task<long>>>(fun =>
            {
                var (arg1, arg2) = fun;
                var temp = DeclareVariable<long>("tmp");
                Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await());
                Return((U)temp + 20L);
            });
            var fn = lambda.Compile();
            Equal(35L, fn(5L, 10L).Result);
        }

        [Fact]
        public static void SimpleAsyncLambdaValueTask()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambda<Func<long, long, ValueTask<long>>>(fun =>
            {
                var (arg1, arg2) = fun;
                var temp = DeclareVariable<long>("tmp");
                Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await());
                Return((U)temp + 20L);
            });
            var fn = lambda.Compile();
            Equal(35L, fn(5L, 10L).Result);
        }

        [Fact]
        public static void AsyncLambdaWithConditional()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambda<Func<long, Task<long>>>(fun =>
            {
                var arg = (U)fun[0];
                If(arg > 10L)
                    .Then(() => Return(Expression.Call(null, sumMethod, arg, 10L.Const()).Await()))
                    .Else(() =>
                    {
                        var local = DeclareVariable<long>("myVar");
                        Assign(local, Expression.Call(null, sumMethod, arg, 90L.Const()).Await());
                        Return(local);
                    })
                    .End();
            });
            var fn = lambda.Compile();
            Equal(25L, fn(15L).Result);
            Equal(99L, fn(9L).Result);
        }

        [Fact]
        public static void TryFinallyAsync()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambda<Func<long[], Task<long>>>(fun =>
            {
                var result = DeclareVariable<long>("accumulator");
                ForEach(fun[0], item =>
                {
                    If((U)item == 0L).Then(Break).End();
                    Assign(result, Expression.Call(null, sumMethod, result, item).Await());
                });
                Return(result);
            });
            var fn = lambda.Compile();
            Equal(15L, fn(new[] { 3L, 2L, 10L }).Result);
            Equal(5, fn(new[] { 3L, 2L, 0L, 10L }).Result);
        }

        [Fact]
        public static void TryCatchAsyncLambdaTest()
        {
            var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var lambda = AsyncLambda<Func<long, Task<long>>>(fun =>
            {
                var arg = (U)fun[0];
                Try(() =>
                {
                    If(arg < 0L).Then(Throw<InvalidOperationException>).End();
                    If(arg > 10L).Then(Throw<ArgumentException>).Else(() => Return(arg)).End();
                })
                .Catch<ArgumentException>(() => Return(ExpressionBuilder.Const(-42L)))
                .Catch<InvalidOperationException>(Rethrow)
                .End();
            });
            var fn = lambda.Compile();
            Equal(5L, fn(5L).Result);
            Equal(-42L, fn(80L).Result);
            var exception = Throws<AggregateException>(() => fn(-10L).Result);
            IsType<InvalidOperationException>(exception.InnerException);
        }

        [Fact]
        public static void LeaveAsyncTryCatchTest()
        {
            var lambda = AsyncLambda<Func<long[], Task<string>>>(fun =>
            {
                For(0.Const(), i => (U)i < fun[0].ArrayLength(), PostIncrementAssign, i =>
                {
                    Using(typeof(MemoryStream).New(), Break);
                });
            });
            var fn = lambda.Compile();
            Null(fn(new[] { 1L }).Result);
        }
    }
}