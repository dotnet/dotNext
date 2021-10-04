using System.Linq.Expressions;

namespace DotNext.Reflection;

/// <summary>
/// Indicates that requested operator doesn't exist.
/// </summary>
public sealed class MissingOperatorException : ConstraintViolationException
{
    /// <summary>
    /// Initializes a new exception indicating that requested operator doesn't exist.
    /// </summary>
    /// <param name="target">The inspected type.</param>
    /// <param name="operator">Missing operator.</param>
    public MissingOperatorException(Type target, ExpressionType @operator)
        : base(target, ExceptionMessages.MissingOperator(@operator))
    {
    }

    internal static MissingOperatorException Create<T>(UnaryOperator @operator)
        => new(typeof(T), @operator.ToExpressionType());

    internal static MissingOperatorException Create<T>(BinaryOperator @operator)
        => new(typeof(T), @operator.ToExpressionType());
}