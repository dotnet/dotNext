using Expression = System.Linq.Expressions.Expression;

namespace DotNext.Linq.Expressions
{
    internal interface IExpressionBuilder<out TExpression>
        where TExpression : notnull, Expression
    {
        TExpression Build();
    }
}