using System;
using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	public abstract class Operator
	{
		private protected Operator(ExpressionType type)
		{
			Type = type;
		}

		/// <summary>
		/// Gets type of operator.
		/// </summary>
		public ExpressionType Type { get; }

		private protected static ExpressionType ToExpressionType(UnaryOperator @operator) => (ExpressionType)@operator;

		private static Expression<D> Unary<D>(UnaryOperator @operator, ParameterExpression param, Expression operand)
			where D: Delegate
		{
			try
			{
				return Expression.Lambda<D>(Expression.MakeUnary(ToExpressionType(@operator), operand, null), param);
			}
			catch(InvalidOperationException)
			{
				var lookup = operand.Type.BaseType;
				return lookup is null ? null : Unary<D>(@operator, param, Expression.Convert(param, lookup));
			}
		}

		private protected static Expression<D> MakeUnary<D>(UnaryOperator @operator, ParameterExpression operand)
			where D : Delegate
			=> Unary<D>(@operator, operand, operand);
	}
}
