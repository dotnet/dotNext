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
            Expression ret = new TaskExpression(90.AsConst());
            Equal(typeof(Task<int>), ret.Type);
            ret = ret.Reduce();
            IsType<TryExpression>(ret);
            Equal(typeof(Task<int>), ret.Type);
        }

        [Fact]
        public void VoidReturnTest()
        {
            Expression ret = new TaskExpression(Expression.Block(typeof(void), 42.AsConst()));
            Equal(typeof(Task), ret.Type);
            ret = ret.Reduce();
            IsType<TryExpression>(ret);
            Equal(typeof(Task), ret.Type);
        }
    }
}
