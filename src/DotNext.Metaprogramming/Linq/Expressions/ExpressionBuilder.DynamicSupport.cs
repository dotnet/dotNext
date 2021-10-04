using System.Dynamic;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

public static partial class ExpressionBuilder
{
    private sealed class MetaExpressionProvider : ISupplier<Expression>, IDynamicMetaObjectProvider
    {
        private readonly Expression expression;

        internal MetaExpressionProvider(Expression? expression)
            => this.expression = expression ?? Expression.Empty();

        /// <inheritdoc/>
        Expression ISupplier<Expression>.Invoke() => expression;

        /// <inheritdoc/>
        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => new MetaExpression(parameter, this);
    }

    /// <summary>
    /// Converts expression to its dynamic representation that allows
    /// to construct expression trees using native language expressions.
    /// </summary>
    /// <param name="expression">The expression to be converted to dynamic expression builder.</param>
    /// <returns>The dynamic representation of expression.</returns>
    public static dynamic AsDynamic(this Expression? expression) => new MetaExpressionProvider(expression);
}