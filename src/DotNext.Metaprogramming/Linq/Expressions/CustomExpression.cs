using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents custom expression.
/// </summary>
public abstract class CustomExpression : Expression
{
    /// <summary>
    /// Initializes a new custom expression node.
    /// </summary>
    protected CustomExpression()
    {
    }

    /// <summary>
    /// Expression type. Always returns <see cref="ExpressionType.Extension"/>.
    /// </summary>
    public sealed override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>
    /// Indicates that this expression can be reduced to well-known LINQ expression.
    /// </summary>
    public sealed override bool CanReduce => true;

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public abstract override Expression Reduce();
}