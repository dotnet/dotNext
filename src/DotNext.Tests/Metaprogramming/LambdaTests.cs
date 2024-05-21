using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Metaprogramming;

using Linq.Expressions;
using static CodeGenerator;
using static Threading.Tasks.Synchronization;

public sealed class LambdaTests : Test
{
    private static long Fact(long value)
    {
        return value > 1L ? value * Fact(value - 1) : value;
    }

    [Fact]
    public static void Recursion()
    {
        var fact = Lambda<Func<long, long>>(static fun =>
        {
            var arg = fun[0];
            If((Expression)(arg.AsDynamic() > 1L)).Then(arg.AsDynamic() * fun.Invoke(arg.AsDynamic() - 1L)).Else(arg).OfType<long>().End();
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
            Assign(result, new AsyncResultExpression(arg1.AsDynamic() + arg2, false));
        });
        Equal(42, lambda.Compile().Invoke(40, 2).GetResult(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public static async Task ThrowInCatchBlock()
    {
        var result = new StrongBox<int>();
        var lambda = AsyncLambda<Func<StrongBox<int>, Task>>(fun =>
        {
            Try(() =>
            {
                Await(Expression.Call(typeof(Task), nameof(Task.Yield), []));
                Throw<InvalidOperationException>();
            })
            .Catch(typeof(InvalidOperationException), e =>
            {
                Throw<ArithmeticException>();
            })
            .Finally(() =>
            {
                Assign(Expression.Field(fun[0], "Value"), 45.Const());
            })
            .End();
        }).Compile();

        await ThrowsAsync<ArithmeticException>(() => lambda(result));
        Equal(45, result.Value);
    }

    [Fact]
    public static void AsyncLambdaWithoutAwaitValueTask()
    {
        var lambda = Lambda<Func<int, int, ValueTask<int>>>((fun, result) =>
        {
            var (arg1, arg2) = fun;
            Assign(result, new AsyncResultExpression(arg1.AsDynamic() + arg2, true));
        });
        Equal(42, lambda.Compile().Invoke(40, 2).AsTask().GetResult(TimeSpan.FromMinutes(1)));
    }

    private static Task<long> Sum(long x, long y)
        => Task.FromResult(x + y);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SimpleAsyncLambda(bool usePooling)
    {
        var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var lambda = AsyncLambda<Func<long, long, Task<long>>>(usePooling, fun =>
        {
            var (arg1, arg2) = fun;
            var temp = DeclareVariable<long>("tmp");
            Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await(true));
            Return(temp.AsDynamic() + 20L);
        });
        var fn = lambda.Compile();
        Equal(35L, fn(5L, 10L).GetResult(TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SimpleUntypedAsyncLambda(bool usePooling)
    {
        var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var flags = usePooling ? AsyncLambdaFlags.UseTaskPooling : AsyncLambdaFlags.None;
        var lambda = AsyncLambda(new[] { typeof(long), typeof(long) }, typeof(long), flags, fun =>
       {
           var (arg1, arg2) = fun;
           var temp = DeclareVariable<long>("tmp");
           Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await(true));
           Return(temp.AsDynamic() + 20L);
       });
        var fn = lambda.Compile() as Func<long, long, Task<long>>;
        NotNull(fn);
        Equal(35L, fn(5L, 10L).GetResult(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public static void SimpleAsyncLambdaThrowsException()
    {
        var lambda = AsyncLambda<Func<Task<long>, long, Task<long>>>(static fun =>
        {
            var (arg1, arg2) = fun;
            Return(arg1.Await().Add(arg2));
        });

        var fn = lambda.Compile();

        var source = new TaskCompletionSource<long>();
        var result = fn(source.Task, 115L);
        False(result.IsCompleted);
        source.SetException(new ApplicationException());
        var e = Throws<AggregateException>(() => result.Result);
        IsType<ApplicationException>(e.InnerException);
    }

    [Fact]
    public static void SimpleAsyncLambdaImplicitResult()
    {
        var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var lambda = AsyncLambda<Func<long, long, Task<long>>>((fun, result) =>
        {
            var (arg1, arg2) = fun;
            var temp = DeclareVariable<long>("tmp");
            Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await(true));
            Assign(result, temp.AsDynamic() + 20L);
        });
        var fn = lambda.Compile();
        Equal(35L, fn(5L, 10L).GetResult(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public static void SimpleAsyncLambdaImplicitResult2()
    {
        var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var lambda = AsyncLambda<Func<long, long, Task<long>>>((fun, result) =>
        {
            var (arg1, arg2) = fun;
            var temp = DeclareVariable<long>("tmp");
            Assign(temp, Expression.Call(null, sumMethod, arg1, arg2).Await(true));
            Assign(result, temp.AsDynamic() + 20L);
            Return();
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
            Return(temp.AsDynamic() + 20L);
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
            var arg = fun[0];
            If((Expression)(arg.AsDynamic() > 10L))
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
                If(((Expression)(item.AsDynamic() == 0L))).Then(Break).End();
                Assign(result, Expression.Call(null, sumMethod, result, item).Await(true));
            });
            Return(result);
        });
        var fn = lambda.Compile();
        Equal(15L, fn(new[] { 3L, 2L, 10L }).GetResult(TimeSpan.FromMinutes(1)));
        Equal(5, fn(new[] { 3L, 2L, 0L, 10L }).GetResult(TimeSpan.FromMinutes(1)));
    }

    private sealed class FinallyCallback : StrongBox<bool>
    {
        internal void OnFinally() => Value = true;
    }

    [Fact]
    public static void TryFinallyWithException()
    {
        var lambda = AsyncLambda<Func<Task, Action, Task>>(static fun =>
        {
            Try(() => Await(fun[0])).Finally(fun[1].Invoke()).End();
        });

        var fn = lambda.Compile();
        var source = new TaskCompletionSource<int>();
        var callback = new FinallyCallback();
        var result = fn(source.Task, callback.OnFinally);
        source.SetException(new ApplicationException());
        Throws<ApplicationException>(result.GetAwaiter().GetResult);
        True(callback.Value);
    }

    [Fact]
    public static void TryCatchAsyncLambdaTest()
    {
        var sumMethod = typeof(LambdaTests).GetMethod(nameof(Sum), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var lambda = AsyncLambda<Func<long, Task<long>>>(fun =>
        {
            var arg = fun[0];
            Try(() =>
            {
                If((Expression)(arg.AsDynamic() < 0L)).Then(Throw<InvalidOperationException>).End();
                If((Expression)(arg.AsDynamic() > 10L)).Then(Throw<ArgumentException>).Else(() => Return(arg)).End();
            })
            .Catch<ArgumentException>(() => Return((-42L).Const()))
            .Catch<InvalidOperationException>(Rethrow)
            .End();
        });
        var fn = lambda.Compile();
        Equal(5L, fn(5L).GetResult(TimeSpan.FromMinutes(1)));
        Equal(-42L, fn(80L).GetResult(TimeSpan.FromMinutes(1)));
        var exception = fn(-10L).GetResult(TimeSpan.FromMinutes(1)).Error;
        IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public static void LeaveAsyncTryCatchTest()
    {
        var lambda = AsyncLambda<Func<long[], Task<string>>>(static fun =>
        {
            For(0.Const(), i => i.AsDynamic() < fun[0].ArrayLength(), PostIncrementAssign, static i =>
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
        var lambda = AsyncLambda<Func<Task<int>>>(static fun =>
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
    public static async Task AsyncWithoutReturnTypeValueTask()
    {
        var lambda = AsyncLambda<Func<StringBuilder, ValueTask>>(static fun =>
        {
            Await(typeof(Task).CallStatic(nameof(Task.Delay), 0.Const()));
            Call(fun[0], "Append", "Hello, world!".Const());
        }).Compile();
        var builder = new StringBuilder(40);
        await lambda(builder);
        Equal("Hello, world!", builder.ToString());
    }

    [Fact]
    public static async Task AsyncWithoutReturnType()
    {
        var lambda = AsyncLambda<Func<StringBuilder, Task>>(static fun =>
        {
            Await(typeof(Task).CallStatic(nameof(Task.Delay), 0.Const()));
            Call(fun[0], "Append", "Hello, world!".Const());
            Return();
        }).Compile();
        var builder = new StringBuilder(40);
        await lambda(builder);
        Equal("Hello, world!", builder.ToString());
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
        var lambda = Lambda<Action<string>>(static fun =>
        {
            WriteLine(fun[0]);
        }).Compile();
        lambda("Hello");
    }

    [Fact]
    public static void RegressionIssue19CharType()
    {
        var lambda = Lambda<Action<string>>(static fun =>
        {
            ForEach(fun[0], WriteLine);
        }).Compile();
        lambda("Hello");
    }

    [Fact]
    public static void RegressionIssue19GuidType()
    {
        var lambda = Lambda<Action<Guid>>(static fun =>
        {
            WriteLine(fun[0]);
        }).Compile();
        lambda(Guid.Empty);
    }

    [Fact]
    public static void RegressionIssue19RefType()
    {
        var lambda = Lambda<Action<Type>>(static fun =>
        {
            WriteLine(fun[0]);
        }).Compile();
        lambda(typeof(object));
    }

    [Fact]
    public static void WriteLineToError()
    {
        var lambda = Lambda<Action<string>>(static fun =>
        {
            WriteError(fun[0]);
        }).Compile();
        lambda("Error");
    }

    [Fact]
    public static void WriteDebugMessage()
    {
        var lambda = Lambda<Action<string>>(static fun =>
        {
            DebugMessage(fun[0]);
        }).Compile();
        lambda("Debug Message");
    }

    [Fact]
    public static void ExpressionInlining()
    {
        var lambda = Lambda<Func<long, long, long>>(static fun => ExpressionBuilder.Fragment<Func<long, long, long>>((a, b) => Math.Max(a, b), fun[0], fun[1])).Compile();
        Equal(10, lambda(5, 10));
    }

    [Fact]
    public static async Task RegressionIssue70()
    {
        var exprThrowException = new Func<Task<string>>(ThrowException).Method;
        var exprReprocess = new Func<Task<string>>(Reprocess).Method;

        var asyncTryCatchExpression = AsyncLambda<Func<Task<string>>>((lambdaContext, result) =>
        {
            Try(() =>
            {
                Assign(result, Expression.Call(null, exprThrowException).Await());
                Return(result);
            })
            .Catch<Exception>(() =>
            {
                Assign(result, Expression.Call(null, exprReprocess).Await());
                Return(result);
            })
            .End();
        });

        var asyncTryCatchFunc = asyncTryCatchExpression.Compile();

        using var cancellationTokenSource = new CancellationTokenSource(DefaultTimeout);
        var task = await Task.WhenAny(asyncTryCatchFunc(), Task.Delay(-1, cancellationTokenSource.Token));
        True(task.IsCompletedSuccessfully);
        Equal("Hello, world!", (string)task.GetResult(DefaultTimeout).Value);

        static async Task<string> ThrowException()
        {
            await Task.Yield();
            throw new Exception();
        }

        static Task<string> Reprocess() => Task.FromResult("Hello, world!");
    }

    [Fact]
    public static async Task RegressionIssue127()
    {
        var lambda = AsyncLambda<Func<Task<string>>>(_ =>
        {
            var hello = DeclareVariable<string>("hello");
            var foo = DeclareVariable<Func<string>>("foo");
            Assign(hello, "hello".Const());
            Assign(foo, Expression.Lambda<Func<string>>(hello));
            Return(foo.Invoke());
        });

        Equal("hello", await lambda.Compile().Invoke());
    }

    [Fact]
    public static async Task ParameterClosure()
    {
        var lambda = AsyncLambda<Func<string, Task<string>>>(ctx =>
        {
            var hello = DeclareVariable<string>("hello");
            var foo = DeclareVariable<Func<string>>("foo");
            Assign(hello, "hello".Const());
            Assign(foo, Expression.Lambda<Func<string>>(typeof(string).CallStatic(nameof(string.Concat), hello, ctx[0])));
            Return(foo.Invoke());
        });

        Equal("hello, world", await lambda.Compile().Invoke(", world"));
    }

    [Fact]
    public static async Task RegressionIssue234()
    {
        var asyncMethod = new Func<Task>(DoAsync).Method;
        var lambda = AsyncLambda<Func<ValueTask<int>>>(usePooling: true, (ctx, result) =>
        {
            Await(Expression.Call(asyncMethod));
            Assign(result, 42.Const());
        }).Compile();

        Equal(42, await lambda.Invoke());
        Equal(42, await lambda.Invoke());
        Equal(42, await lambda.Invoke());
        Equal(42, await lambda.Invoke());

        static Task DoAsync() => Task.Delay(1);
    }
}