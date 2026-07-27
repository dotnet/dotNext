using System.Dynamic;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

public static partial class ExpressionBuilder
{
    /// <summary>
    /// Converts expression to its dynamic representation that allows
    /// to construct expression trees using native language expressions.
    /// </summary>
    /// <param name="expression">The expression to be converted to dynamic expression builder.</param>
    /// <returns>The dynamic representation of expression.</returns>
    [Obsolete("Use overloaded extension operators and quoted expressions instead.")]
    public static dynamic AsDynamic(this Expression? expression) => new MetaExpressionProvider(expression);
}

[Obsolete]
file sealed class MetaExpressionProvider(Expression? expression) : ISupplier<Expression>, IDynamicMetaObjectProvider
{
    private readonly Expression expression = expression ?? Expression.Empty();

    /// <inheritdoc/>
    Expression ISupplier<Expression>.Invoke() => expression;

    /// <inheritdoc/>
    DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => new MetaExpression(parameter, this);
}