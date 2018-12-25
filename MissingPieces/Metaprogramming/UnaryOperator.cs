using System;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
    /// <summary>
    /// Represents unary operator.
    /// </summary>
    public enum UnaryOperator : int
    {
        Plus = ExpressionType.UnaryPlus,

        Negate = ExpressionType.Negate,

		Convert = ExpressionType.Convert,

		ConvertChecked = ExpressionType.ConvertChecked
    }

    /// <summary>
    /// Represents unary operator applicable to type <typeparamref name="T"/>.
    /// </summary>
	/// <typeparam name="T">Target type.</typeparam>
    /// <typeparam name="R">Type of unary operator result.</typeparam>
	[DefaultMember("Invoke")]
    public sealed class UnaryOperator<T, R> : Operator<UnaryOperator<T, R>.Invoker>
    {
		/// <summary>
		/// A delegate representing unary operator.
		/// </summary>
		/// <param name="operand">An operand.</param>
		/// <returns>Result of unary operator.</returns>
		public delegate R Invoker(in T operand);

        private UnaryOperator(Expression<Invoker> invoker, UnaryOperator type)
            : base(invoker.Compile(), ToExpressionType(type))
        {
            Type = type;
        }

        /// <summary>
        /// Type of operator.
        /// </summary>
        public new UnaryOperator Type { get; }

		/// <summary>
		/// Invokes unary operator.
		/// </summary>
		/// <param name="operand">An operand.</param>
		/// <returns>Result of unary operator.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public R Invoke(in T operand) => invoker(in operand);

		private static Expression<Invoker> MakeUnary(UnaryOperator @operator, ParameterExpression param, Expression operand)
		{
			try
			{
				return Expression.Lambda<Invoker>(Expression.MakeUnary(ToExpressionType(@operator), operand, typeof(R)), param);
			}
			catch(ArgumentException e)
			{
				Debug.WriteLine(e);
				return null;
			}
			catch(InvalidOperationException)
			{
				//do not walk through inheritance hierarchy for value types
				if(param.Type.IsValueType) return null;
				var lookup = operand.Type.BaseType;
				return lookup is null ? null : MakeUnary(@operator, param, Expression.Convert(param, lookup));
			}
		}

		/// <summary>
		/// Reflects unary operator.
		/// </summary>
		/// <param name="@operator">Type of operator.</param>
		/// <returns></returns>
        public static UnaryOperator<T, R> Reflect(UnaryOperator @operator)
		{
			var parameter = Expression.Parameter(typeof(T).MakeByRefType());
			var result = MakeUnary(@operator, parameter, parameter);
            return result is null ? null : new UnaryOperator<T, R>(result, @operator);
		}
    }
}
