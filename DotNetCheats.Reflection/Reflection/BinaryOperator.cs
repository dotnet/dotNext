using System.Linq.Expressions;

namespace Cheats.Reflection
{
	/// <summary>
	/// Represents binary operator.
	/// </summary>
	public enum BinaryOperator
	{
		/// <summary>
		/// An addition operation, such as a + b, without overflow checking.
		/// </summary>
		Add = ExpressionType.Add,

		/// <summary>
		/// An addition operation, such as (a + b), with overflow checking.
		/// </summary>
		AddChecked = ExpressionType.AddChecked,

		/// <summary>
		/// a & b
		/// </summary>
		And = ExpressionType.And,

		/// <summary>
		/// a / b
		/// </summary>
		Divide = ExpressionType.Divide,

		/// <summary>
		/// a == b
		/// </summary>
		Equal = ExpressionType.Equal,

		/// <summary>
		/// a ^ b
		/// </summary>
		ExclusiveOr = ExpressionType.ExclusiveOr,

		/// <summary>
		/// a > b
		/// </summary>
		GreaterThan = ExpressionType.GreaterThan,

		/// <summary>
		/// a >= b
		/// </summary>
		GreaterThanOrEqual = ExpressionType.GreaterThanOrEqual,

		/// <summary>
		/// a ?? b
		/// </summary>
		Coalesce = ExpressionType.Coalesce
	}
}
