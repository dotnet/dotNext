using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Represents unary operator.
	/// </summary>
	public enum UnaryOperator: int
	{
		Plus = ExpressionType.UnaryPlus,

		Negate = ExpressionType.Negate
	}

	/// <summary>
	/// Represents unary operator.
	/// </summary>
	/// <typeparam name="I">Type of operand.</typeparam>
	/// <typeparam name="O">Type of result.</typeparam>
	/// <param name="operand">Operand.</param>
	/// <returns>Result.</returns>
	public delegate O UnaryOperator<I, out O>(in I operand);
}
