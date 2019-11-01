using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;
    using static Threading.Tasks.Synchronization;
    using U = Linq.Expressions.UniversalExpression;

    [ExcludeFromCodeCoverage]
    public sealed class LambdaTests : Assert
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
            Equal(42, lambda.Compile().Invoke(40, 2).GetResult(TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public static void AsyncLambdaWithoutAwaitValueTask()
        {
            var lambda = Lambda<Func<int, int, ValueTask<int>>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Assign(result, new AsyncResultExpression((U)arg1 + arg2, true));
            });
            Equal(42, lambda.Compile().Invoke(40, 2).AsTask().GetResult(TimeSpan.FromMinutes(1)));
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
                Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await(true));
                Return((U)temp + 20L);
            });
            var fn = lambda.Compile();
            Equal(35L, fn(5L, 10L).GetResult(TimeSpan.FromMinutes(1)));
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
            Equal(35L, fn(5L, 10L).AsTask().GetResult(TimeSpan.FromMinutes(1)));
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
            Equal(25L, fn(15L).GetResult(TimeSpan.FromMinutes(1)));
            Equal(99L, fn(9L).GetResult(TimeSpan.FromMinutes(1)));
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
                    Assign(result, Expression.Call(null, sumMethod, result, item).Await(true));
                });
                Return(result);
            });
            var fn = lambda.Compile();
            Equal(15L, fn(new[] { 3L, 2L, 10L }).GetResult(TimeSpan.FromMinutes(1)));
            Equal(5, fn(new[] { 3L, 2L, 0L, 10L }).GetResult(TimeSpan.FromMinutes(1)));
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
                .Catch<ArgumentException>(() => Return((-42L).Const()))
                .Catch<InvalidOperationException>(Rethrow)
                .End();
            });
            var fn = lambda.Compile();
            Equal(5L, fn(5L).GetResult(TimeSpan.FromMinutes(1)));
            Equal(-42L, fn(80L).GetResult(TimeSpan.FromMinutes(1)));
            var exception = fn(-10L).GetResult(TimeSpan.FromMinutes(1)).Error;
            IsType<AggregateException>(exception);
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
            Null(fn(new[] { 1L }).GetResult(TimeSpan.FromMinutes(1)).Value);
        }

        [Fact]
        public static void AwaitStatements()
        {
            var lambda = AsyncLambda<Func<Task<int>>>(fun =>
            {
                var result = DeclareVariable("result", "42".Const());
                Await(typeof(Task).CallStatic(nameof(Task.Delay), 0.Const()));
                Assign(result, result.Concat("3".Const()));
                Await(typeof(Task).CallStatic(nameof(Task.Delay), 100.Const()));
                Return(typeof(int).CallStatic(nameof(int.Parse), result));
            }).Compile();
            Equal(423, lambda().GetResult(TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public static void StringFormatting()
        {
            var lambda = Lambda<Func<string, string>>((fun, result) =>
            {
                Assign(result, InterpolationExpression.PlainString($"Hello, {fun[0]}"));
            }).Compile();
            Equal("Hello, Barry", lambda("Barry"));
        }

        [Fact]
        public static void FormattableStringFactory()
        {
            var lambda = Lambda<Func<string, FormattableString>>((fun, result) =>
            {
                Assign(result, InterpolationExpression.FormattableString($"Hello, {fun[0]}"));
            }).Compile();
            Equal("Hello, Barry", lambda("Barry").ToString());
        }

        [Fact]
        public static void WriteLineToOut()
        {
            var lambda = Lambda<Action<string>>(fun =>
            {
                WriteLine(fun[0]);
            }).Compile();
            lambda("Hello");
        }

        [Fact]
        public static void WriteLineToError()
        {
            var lambda = Lambda<Action<string>>(fun =>
            {
                WriteError(fun[0]);
            }).Compile();
            lambda("Error");
        }

        [Fact]
        public static void WriteDebugMessage()
        {
            var lambda = Lambda<Action<string>>(fun =>
            {
                DebugMessage(fun[0]);
            }).Compile();
            lambda("Debug Message");
        }

        [Fact]
        public static void ExpressionInlining()
        {
            var lambda = Lambda<Func<long, long, long>>(fun => ExpressionBuilder.Fragment<Func<long, long, long>>((a, b) => Math.Max(a, b), fun[0], fun[1])).Compile();
            Equal(10, lambda(5, 10));
        }
    }
}