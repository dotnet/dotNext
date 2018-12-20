using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Represents unary operator.
	/// </summary>
	public enum UnaryOperator: int
	{
		Plus = ExpressionType.UnaryPlus,

		Negate = ExpressionType.Negate,
		Convert = ExpressionType.Convert,
		ConvertChecked = ExpressionType.ConvertChecked,
	}
}
