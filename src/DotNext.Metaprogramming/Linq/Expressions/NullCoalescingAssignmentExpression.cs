using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents null-coalescing assignment operator.
/// </summary>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/null-coalescing-assignment">Null-coalescing assignment.</seealso>
public sealed class NullCoalescingAssignmentExpression : CustomExpression
{
    internal NullCoalescingAssignmentExpression(Expression left, Expression right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (!left.Type.IsAssignableFrom(right.Type))
            throw new ArgumentException(ExceptionMessages.TypeExpected(left.Type), nameof(right));

        Left = left;
        Right = right;
    }

    /// <summary>
    /// Initializes a new assignment expression.
    /// </summary>
    /// <param name="left">The left operand of the assignment.</param>
    /// <param name="right">The right operand of the assignment.</param>
    /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
    public NullCoalescingAssignmentExpression(ParameterExpression left, Expression right)
        : this(left.As<Expression>(), right)
    {
    }

    /// <summary>
    /// Initializes a new assignment expression.
    /// </summary>
    /// <param name="left">The left operand of the assignment.</param>
    /// <param name="right">The right operand of the assignment.</param>
    /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
    public NullCoalescingAssignmentExpression(MemberExpression left, Expression right)
        : this(left.As<Expression>(), right)
    {
    }

    /// <summary>
    /// Initializes a new assignment expression.
    /// </summary>
    /// <param name="left">The left operand of the assignment.</param>
    /// <param name="right">The right operand of the assignment.</param>
    /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
    public NullCoalescingAssignmentExpression(IndexExpression left, Expression right)
        : this(left.As<Expression>(), right)
    {
    }

    /// <summary>
    /// Gets the left operand of the assignment operation.
    /// </summary>
    public Expression Left { get; }

    /// <summary>
    /// Gets the right operand of the assignment operation.
    /// </summary>
    public Expression Right { get; }

    /// <summary>
    /// Gets result type of asynchronous operation.
    /// </summary>
    public override Type Type => Left.Type;

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        if (Left.Type.IsValueType && Nullable.GetUnderlyingType(Left.Type) is null || Left.Type.IsPrimitive || Left.Type.IsPointer)
            return Left;

        if (Left is ParameterExpression localVar)
            return Build(localVar, Right);

        localVar = Variable(Left.Type);

        return Block(
            Left.Type,
            new[] { localVar },
            Assign(localVar, Left),
            Assign(Left, Build(localVar, Right)),
            localVar);

        static Expression Build(ParameterExpression left, Expression right)
            => Coalesce(left, Assign(left, right));
    }

    /// <summary>
    /// Visit children expressions.
    /// </summary>
    /// <param name="visitor">Expression visitor.</param>
    /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var left = visitor.Visit(Left);
        var right = visitor.Visit(Right);
        return ReferenceEquals(left, Left) && ReferenceEquals(right, Right) ? this : new NullCoalescingAssignmentExpression(left, right);
    }
}