using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Metaprogramming;

using Linq.Expressions;
using static CodeGenerator;

// verifies that the evaluation order and the side effects of the async lambda match C# semantics
public sealed class AsyncLambdaLoweringTests : Test
{
    public sealed class Holder
    {
        public int Field;
    }

    public sealed class Recorder(List<string> log)
    {
        public readonly List<string> Items = [];

        public Recorder Inner => field ??= new(log);

        public string A
        {
            get;
            set
            {
                log.Add("set " + value);
                field = value;
            }
        }

        public string B
        {
            get;
            set
            {
                log.Add("set " + value);
                field = value;
            }
        }
    }

    public struct Counter
    {
        public int Value;

        public void Add(int value) => Value += value;
    }

    private static string Log(List<string> log, string value)
    {
        log.Add(value);
        return value;
    }

    private static async Task<string> LogAsync(List<string> log, string value)
    {
        log.Add(value);
        await Task.Yield();
        return value;
    }

    private static async Task<bool> LogBoolAsync(List<string> log, string value, bool result)
    {
        log.Add(value);
        await Task.Yield();
        return result;
    }

    private static async Task<int> IntAsync(int value)
    {
        await Task.Yield();
        return value;
    }

    private static async Task<string> EchoAsync(string value)
    {
        await Task.Yield();
        return value;
    }

    private static Holder GetHolder(List<string> log, Holder holder)
    {
        log.Add("holder");
        return holder;
    }

    private static MethodInfo GetMethod(string name)
        => typeof(AsyncLambdaLoweringTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Expression LogExpr(Expression log, string value)
        => Expression.Call(GetMethod(nameof(Log)), log, Expression.Constant(value));

    private static AwaitExpression LogAsyncExpr(Expression log, string value)
        => Expression.Call(GetMethod(nameof(LogAsync)), log, Expression.Constant(value)).Await();

    private static AwaitExpression IntAsyncExpr(Expression value)
        => Expression.Call(GetMethod(nameof(IntAsync)), value).Await();

    private static AwaitExpression FromResult(Expression value)
        => Expression.Call(typeof(Task), nameof(Task.FromResult), [value.Type], value).Await();

    private static MethodInfo Concat2 => typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!;

    private static MethodInfo Concat3 => typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string), typeof(string)])!;

    [Fact]
    public static async Task ArgumentsEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            Return(Expression.Call(Concat3, LogExpr(fun[0], "A"), LogAsyncExpr(fun[0], "B"), LogExpr(fun[0], "C")));
        }).Compile();

        var log = new List<string>();
        Equal("ABC", await lambda(log));
        Equal(["A", "B", "C"], log);
    }

    [Fact]
    public static async Task MultipleAwaitsInArguments()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            Return(Expression.Call(Concat3, LogAsyncExpr(fun[0], "A"), LogAsyncExpr(fun[0], "B"), LogAsyncExpr(fun[0], "C")));
        }).Compile();

        var log = new List<string>();
        Equal("ABC", await lambda(log));
        Equal(["A", "B", "C"], log);
    }

    [Fact]
    public static async Task NestedAwaitInArgument()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            var echo = Expression.Call(GetMethod(nameof(EchoAsync)), LogAsyncExpr(fun[0], "B")).Await();
            Return(Expression.Call(Concat2, LogAsyncExpr(fun[0], "A"), echo));
        }).Compile();

        var log = new List<string>();
        Equal("AB", await lambda(log));
        Equal(["A", "B"], log);
    }

    [Fact]
    public static async Task ReceiverEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            var insert = typeof(string).GetMethod(nameof(string.Insert), [typeof(int), typeof(string)])!;
            Return(Expression.Call(LogExpr(fun[0], "R"), insert, Expression.Constant(1), LogAsyncExpr(fun[0], "X")));
        }).Compile();

        var log = new List<string>();
        Equal("RX", await lambda(log));
        Equal(["R", "X"], log);
    }

    [Fact]
    public static async Task BothBinaryOperandsAwaited()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            Return(Expression.Subtract(IntAsyncExpr(Expression.Constant(10)), IntAsyncExpr(Expression.Constant(3))));
        }).Compile();

        Equal(7, await lambda());
    }

    [Fact]
    public static async Task LocalReadBeforeAwait()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            var x = DeclareVariable("x", Expression.Constant(1));
            Return(Expression.Add(x, FromResult(Expression.Assign(x, Expression.Constant(5)))));
        }).Compile();

        Equal(6, await lambda());
    }

    [Fact]
    public static async Task CompoundAssignmentToLocal()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            var x = DeclareVariable("x", Expression.Constant(1));
            Statement(Expression.AddAssign(x, FromResult(Expression.Assign(x, Expression.Constant(10)))));
            Return(x);
        }).Compile();

        Equal(11, await lambda());
    }

    [Fact]
    public static async Task CompoundAssignmentToField()
    {
        var lambda = AsyncLambda<Func<List<string>, Holder, Task>>(static fun =>
        {
            var field = Expression.Field(Expression.Call(GetMethod(nameof(GetHolder)), fun[0], fun[1]), nameof(Holder.Field));
            Statement(Expression.AddAssign(field, Expression.Call(typeof(int), nameof(int.Parse), [], LogAsyncExpr(fun[0], "2"))));
        }).Compile();

        var log = new List<string>();
        var holder = new Holder { Field = 40 };
        await lambda(log, holder);
        Equal(42, holder.Field);
        Equal(["holder", "2"], log);
    }

    [Fact]
    public static async Task ByRefArgument()
    {
        var lambda = AsyncLambda<Func<Holder, Task<int>>>(static fun =>
        {
            Return(Expression.Call(
                typeof(Interlocked),
                nameof(Interlocked.Exchange),
                [],
                Expression.Field(fun[0], nameof(Holder.Field)),
                IntAsyncExpr(Expression.Constant(42))));
        }).Compile();

        var holder = new Holder { Field = 1 };
        Equal(1, await lambda(holder));
        Equal(42, holder.Field);
    }

    [Fact]
    public static async Task ValueTypeReceiver()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            var counter = DeclareVariable(typeof(Counter), "counter");
            Statement(Expression.Call(counter, nameof(Counter.Add), [], IntAsyncExpr(Expression.Constant(5))));
            Return(Expression.Field(counter, nameof(Counter.Value)));
        }).Compile();

        Equal(5, await lambda());
    }

    [Theory]
    [InlineData(true, "X")]
    [InlineData(false, "Y")]
    public static async Task ConditionalEvaluatesTakenBranchOnly(bool condition, string expected)
    {
        var lambda = AsyncLambda<Func<List<string>, bool, Task<string>>>(static fun =>
        {
            Return(Expression.Condition(fun[1], LogAsyncExpr(fun[0], "X"), LogExpr(fun[0], "Y")));
        }).Compile();

        var log = new List<string>();
        Equal(expected, await lambda(log, condition));
        Equal([expected], log);
    }

    [Theory]
    [InlineData(true, "AX")]
    [InlineData(false, "AY")]
    public static async Task ConditionalAsArgument(bool condition, string expected)
    {
        var lambda = AsyncLambda<Func<List<string>, bool, Task<string>>>(static fun =>
        {
            Return(Expression.Call(Concat2, LogExpr(fun[0], "A"), Expression.Condition(fun[1], LogAsyncExpr(fun[0], "X"), LogExpr(fun[0], "Y"))));
        }).Compile();

        var log = new List<string>();
        Equal(expected, await lambda(log, condition));
        Equal(["A", expected[1..]], log);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    public static async Task AndAlsoShortCircuit(bool left, bool right, bool evaluatesRight)
    {
        var lambda = AsyncLambda<Func<List<string>, bool, bool, Task<bool>>>(static fun =>
        {
            Return(Expression.AndAlso(fun[1], Expression.Call(GetMethod(nameof(LogBoolAsync)), fun[0], Expression.Constant("R"), fun[2]).Await()));
        }).Compile();

        var log = new List<string>();
        Equal(left && right, await lambda(log, left, right));
        Equal(evaluatesRight, log is ["R"]);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public static async Task OrElseShortCircuit(bool left, bool right, bool evaluatesRight)
    {
        var lambda = AsyncLambda<Func<List<string>, bool, bool, Task<bool>>>(static fun =>
        {
            Return(Expression.OrElse(fun[1], Expression.Call(GetMethod(nameof(LogBoolAsync)), fun[0], Expression.Constant("R"), fun[2]).Await()));
        }).Compile();

        var log = new List<string>();
        Equal(left || right, await lambda(log, left, right));
        Equal(evaluatesRight, log is ["R"]);
    }

    [Theory]
    [InlineData("L", "L")]
    [InlineData(null, "R")]
    public static async Task CoalesceShortCircuit(string left, string expected)
    {
        var lambda = AsyncLambda<Func<List<string>, string, Task<string>>>(static fun =>
        {
            Return(Expression.Coalesce(fun[1], LogAsyncExpr(fun[0], "R")));
        }).Compile();

        var log = new List<string>();
        Equal(expected, await lambda(log, left));
        Equal(left is null, log is ["R"]);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(null, 42)]
    public static async Task CoalesceNullableValueType(int? left, int expected)
    {
        var lambda = AsyncLambda<Func<int?, Task<int>>>(static fun =>
        {
            Return(Expression.Coalesce(fun[0], IntAsyncExpr(Expression.Constant(42))));
        }).Compile();

        Equal(expected, await lambda(left));
    }

    [Fact]
    public static async Task ReturnValueComputedBeforeFinally()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            var x = DeclareVariable("x", Expression.Constant(1));
            Try(() => Return(x))
                .Finally(() => Assign(x, Expression.Constant(2)))
                .End();
            Return(Expression.Constant(-1));
        }).Compile();

        Equal(1, await lambda());
    }

    [Fact]
    public static async Task ReturnValueSurvivesAwaitInFinally()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            Try(() => Return(LogExpr(fun[0], "R")))
                .Finally(() => Await(Expression.Call(typeof(Task), nameof(Task.Yield), [])))
                .End();
            Return(Expression.Constant(string.Empty));
        }).Compile();

        Equal("R", await lambda([]));
    }

    [Fact]
    public static async Task InnerLambdaCapturesLocalMultipleTimes()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(static _ =>
        {
            var x = DeclareVariable("x", Expression.Constant(21));
            Await(Expression.Call(typeof(Task), nameof(Task.Yield), []));
            Return(Expression.Invoke(Expression.Lambda<Func<int>>(Expression.Add(x, x))));
        }).Compile();

        Equal(42, await lambda());
    }

    [Fact]
    public static void ConditionalWithBlockBranchIsNotSupported()
    {
        Throws<NotSupportedException>(static () => AsyncLambda<Func<bool, Task<int>>>(static fun =>
        {
            Return(Expression.Condition(fun[0], Expression.Block(typeof(int), Expression.Constant(1)), Expression.Constant(2)));
        }));
    }

    private static NewExpression NewRecorder(Expression log)
        => Expression.New(typeof(Recorder).GetConstructor([typeof(List<string>)])!, log);

    private static MemberAssignment Bind(string member, Expression value)
        => Expression.Bind(typeof(Recorder).GetProperty(member)!, value);

    [Fact]
    public static async Task MemberInitEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<Recorder>>>(static fun =>
        {
            Return(Expression.MemberInit(NewRecorder(fun[0]), Bind(nameof(Recorder.A), LogExpr(fun[0], "A")), Bind(nameof(Recorder.B), LogAsyncExpr(fun[0], "B"))));
        }).Compile();

        var log = new List<string>();
        var result = await lambda(log);
        Equal("A", result.A);
        Equal("B", result.B);
        Equal(["A", "set A", "B", "set B"], log);
    }

    [Fact]
    public static async Task NestedMemberBindingEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<Recorder>>>(static fun =>
        {
            var inner = Expression.MemberBind(
                typeof(Recorder).GetProperty(nameof(Recorder.Inner))!,
                Bind(nameof(Recorder.A), LogExpr(fun[0], "A")),
                Bind(nameof(Recorder.B), LogAsyncExpr(fun[0], "B")));

            Return(Expression.MemberInit(NewRecorder(fun[0]), inner));
        }).Compile();

        var log = new List<string>();
        var result = await lambda(log);
        Equal("A", result.Inner.A);
        Equal("B", result.Inner.B);
        Equal(["A", "set A", "B", "set B"], log);
    }

    [Fact]
    public static async Task MemberListBindingEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<Recorder>>>(static fun =>
        {
            var add = typeof(List<string>).GetMethod(nameof(List<>.Add))!;
            var items = Expression.ListBind(
                typeof(Recorder).GetField(nameof(Recorder.Items))!,
                Expression.ElementInit(add, LogExpr(fun[0], "A")),
                Expression.ElementInit(add, LogAsyncExpr(fun[0], "B")),
                Expression.ElementInit(add, LogExpr(fun[0], "C")));

            Return(Expression.MemberInit(NewRecorder(fun[0]), items));
        }).Compile();

        var log = new List<string>();
        var result = await lambda(log);
        Equal(["A", "B", "C"], result.Items);
        Equal(["A", "B", "C"], log);
    }

    [Fact]
    public static async Task ListInitEvaluationOrder()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<List<string>>>>(static fun =>
        {
            Return(Expression.ListInit(
                Expression.New(typeof(List<string>)),
                LogExpr(fun[0], "A"),
                LogAsyncExpr(fun[0], "B"),
                LogExpr(fun[0], "C")));
        }).Compile();

        var log = new List<string>();
        Equal(["A", "B", "C"], await lambda(log));
        Equal(["A", "B", "C"], log);
    }

    [Fact]
    public static async Task InitializerAsArgument()
    {
        var lambda = AsyncLambda<Func<List<string>, Task<string>>>(static fun =>
        {
            var recorder = Expression.MemberInit(NewRecorder(fun[0]), Bind(nameof(Recorder.B), LogAsyncExpr(fun[0], "B")));
            Return(Expression.Call(Concat2, LogExpr(fun[0], "X"), Expression.Property(recorder, nameof(Recorder.B))));
        }).Compile();

        var log = new List<string>();
        Equal("XB", await lambda(log));
        Equal(["X", "B", "set B"], log);
    }
}
