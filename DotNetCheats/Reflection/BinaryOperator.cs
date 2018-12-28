using System.Linq.Expressions;

namespace MissingPieces.Reflection
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

	/// <summary>
	/// Represents binary operator.
	/// </summary>
	/// <typeparam name="I1">Type of first operand.</typeparam>
	/// <typeparam name="I2">Type of second operand.</typeparam>
	/// <typeparam name="R">Type of result.</typeparam>
	/// <param name="operand1">First operand.</param>
	/// <param name="operand2">Second operand.</param>
	/// <returns>Result.</returns>
	public delegate R BinaryOperator<I1, I2, R>(in I1 operand1, in I2 operand2);
}
