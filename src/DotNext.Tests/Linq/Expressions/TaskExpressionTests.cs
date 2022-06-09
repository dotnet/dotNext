using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

[ExcludeFromCodeCoverage]
public sealed class TaskExpressionTests : Test
{
    [Fact]
    public static void NonVoidReturn()
    {
        Expression ret = new AsyncResultExpression(90.Const(), true);
        Equal(typeof(ValueTask<int>), ret.Type);
        ret = ret.Reduce();
        IsAssignableFrom<UnaryExpression>(ret);
        Equal(typeof(ValueTask<int>), ret.Type);
    }

    [Fact]
    public static void VoidReturn()
    {
        Expression ret = new AsyncResultExpression(Expression.Block(typeof(void), 42.Const()), false);
        Equal(typeof(Task), ret.Type);
        ret = ret.Reduce();
        IsAssignableFrom<UnaryExpression>(ret);
        Equal(typeof(Task), ret.Type);
    }
}