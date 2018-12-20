using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Represents unary operator.
	/// </summary>
	public enum UnaryOperator: int
	{
		Plus = ExpressionType.UnaryPlus,
		Convert = ExpressionType.Convert,
		ConvertChecked = ExpressionType.ConvertChecked,
	}
}
