using System.Threading.Tasks;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class TaskExpressionTests: Assert
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
}
