using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AsyncDelegateTests : Test
{
    private sealed class Accumulator
    {
        private int counter;

        internal int Counter => counter;

        internal void IncBy1() => counter.AddAndGet(1);

        internal void IncBy3() => counter.AddAndGet(3);

        internal void IncBy5() => counter.AddAndGet(5);

        internal void Throw() => throw new Exception();
    }

    [Fact]
    public static async Task InvokeActionAsync()
    {
        var acc = new Accumulator();
        Action action = acc.IncBy1;
        action += acc.IncBy3;
        action += acc.IncBy5;
        await action.InvokeAsync();
        Equal(9, acc.Counter);
    }

    [Fact]
    public static async Task InvokeActionAsyncFailure()
    {
        var acc = new Accumulator();
        Action action = acc.IncBy1;
        action += acc.Throw;
        action += acc.IncBy3;
        await ThrowsAsync<Exception>(async () => await action.InvokeAsync());
    }

    [Fact]
    public static async Task InvokeActionsAsync()
    {
        static MethodInfo GetMethod(int argCount)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var candidate in typeof(AsyncDelegate).GetMethods(flags))
                if (candidate.Name == nameof(AsyncDelegate.InvokeAsync) && candidate.GetParameters().Length == argCount + 2 && candidate.GetParameters()[0].ParameterType.Name.Contains("Action"))
                    return candidate;
            throw new Xunit.Sdk.XunitException();
        }
        var successValue = Expression.Empty();
        var failedValue = Expression.Throw(Expression.New(typeof(ArithmeticException)));
        for (var argCount = 0; argCount <= 10; argCount++)
        {
            var types = new Type[argCount];
            Array.Fill(types, typeof(string));
            var actionType = Expression.GetActionType(types);
            var parameters = new ParameterExpression[argCount];
            parameters.ForEach((ref ParameterExpression p, nint _) => p = Expression.Parameter(typeof(string)));
            //prepare args
            var args = new object[parameters.LongLength + 2];
            Array.Fill(args, string.Empty);
            args[args.LongLength - 1L] = new CancellationToken(false);
            //find method to test
            var method = GetMethod(argCount);
            if (parameters.LongLength > 0L)
                method = method.MakeGenericMethod(types);
            //check success scenario
            args[0] = Expression.Lambda(actionType, successValue, parameters).Compile();
            var result = (Task)method.Invoke(null, args);
            await result;
            //check cancellation
            args[args.LongLength - 1L] = new CancellationToken(true);
            result = (Task)method.Invoke(null, args);
            await ThrowsAsync<TaskCanceledException>(Func.Constant(result));
            //check failure
            args[args.LongLength - 1L] = new CancellationToken(false);
            args[0] = Expression.Lambda(actionType, failedValue, parameters).Compile();
            result = (Task)method.Invoke(null, args);
            await ThrowsAsync<ArithmeticException>(Func.Constant(result));
        }
    }
}