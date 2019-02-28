using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal interface IExpressionBuilder<out E>
        where E: Expression
    {
        E Build();
    }
}