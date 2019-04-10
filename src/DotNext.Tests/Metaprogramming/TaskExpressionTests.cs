using System.Threading.Tasks;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class TaskExpressionTests: Assert
    {
        [Fact]
        public void NonVoidReturnTest()
        {
            Expression ret = new AsyncResultExpression(90.AsConst(), true);
            Equal(typeof(ValueTask<int>), ret.Type);
            ret = ret.Reduce();
            IsAssignableFrom<UnaryExpression>(ret);
            Equal(typeof(ValueTask<int>), ret.Type);
        }

        [Fact]
        public void VoidReturnTest()
        {
            Expression ret = new AsyncResultExpression(Expression.Block(typeof(void), 42.AsConst()), false);
            Equal(typeof(Task), ret.Type);
            ret = ret.Reduce();
            IsAssignableFrom<UnaryExpression>(ret);
            Equal(typeof(Task), ret.Type);
        }
    }
}
