using System;
using System.Linq.Expressions;

namespace MissingPieces.Reflection
{
    /// <summary>
	/// Indicates that requested operator doesn't exist.
	/// </summary>
    public sealed class MissingOperatorException: ConstraintViolationException
    {
        private MissingOperatorException(Type target, ExpressionType @operator)
            : base(target, $"Operator {@operator} doesn't exist in type {target}")
        {
        }

        internal static MissingOperatorException Create<T>(UnaryOperator @operator)
            => new MissingOperatorException(typeof(T), (ExpressionType)@operator);
    }
}