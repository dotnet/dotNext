using Expression = System.Linq.Expressions.Expression;

namespace DotNext.Linq.Expressions
{
    internal interface IExpressionBuilder<out E>
        where E : notnull, Expression
    {
        E Build();
    }
}