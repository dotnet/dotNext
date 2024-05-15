using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using static Linq.Expressions.ExpressionBuilder;
using static Metaprogramming.CodeGenerator;

public sealed class RegressionIssue223 : Test
{
    [Fact]
    public static async Task ThrowOnReturn()
    {
        var lambda = AsyncLambda<Func<Task<int>>>(_ =>
        {
            Try(() =>
                {
                    var methodInfo = new Func<Task<int>>(Throw).Method;
                    var methodResult = Expression.Call(null, methodInfo);

                    Return(methodResult.Await());
                })
                .Catch(typeof(Exception), _ =>
                {
                    CallStatic(typeof(Console), nameof(Console.WriteLine), Expression.Constant("Exception caught"));
                })
                .End();
        });

        var action = lambda.Compile();

        Equal(0, await action());

        static async Task<int> Throw()
        {
            await Task.Yield();
            throw new InvalidOperationException("Exception was not caught");
        }
    }
}