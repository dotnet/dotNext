using System;
using System.Linq.Expressions;

namespace MissingPieces.Reflection
{
    /// <summary>
	/// Indicates that requested operator doesn't exist.
	/// </summary>
    public sealed class MissingOperatorException: ConstraintViolationException
    {
        private MissingOperatorException(ExpressionType @operator, Type target)
            : base($"Operator {@operator} doesn't exist in type {target}", target)
        {
        }

        internal static MissingOperatorException Create<T>(UnaryOperator @operator)
            => new MissingOperatorException((ExpressionType)@operator, typeof(T));
    }
}