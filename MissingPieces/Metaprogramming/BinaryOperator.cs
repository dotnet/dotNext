using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	public enum BinaryOperator
	{
		Add = ExpressionType.Add,
		AddChecked = ExpressionType.AddChecked,
		And = ExpressionType.And,
		Divide = ExpressionType.Divide,
		Equal = ExpressionType.Equal,
		ExclusiveOr = ExpressionType.ExclusiveOr,
		GreaterThan = ExpressionType.GreaterThan,
		GreaterThanOrEqual = ExpressionType.GreaterThanOrEqual
	}
}
