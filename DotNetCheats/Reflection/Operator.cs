using System;
using static System.Diagnostics.Debug;
using System.Linq.Expressions;

namespace MissingPieces.Reflection
{
	public abstract class Operator<D>: IOperator<D>
		where D: Delegate
	{
		private protected readonly D invoker;

		private protected Operator(D invoker, ExpressionType type)
		{
			Type = type;
			this.invoker = invoker;
		}

		D IOperator<D>.Invoker => invoker;

		public static implicit operator D(Operator<D> @operator) => @operator?.invoker;

		/// <summary>
		/// Gets type of operator.
		/// </summary>
		public ExpressionType Type { get; }

		private protected static ExpressionType ToExpressionType(UnaryOperator @operator) => (ExpressionType)@operator;

		private static Expression<D> Convert(ParameterExpression parameter, Expression operand, Type conversionType, bool @checked)
		{
			try
			{
				return Expression.Lambda<D>(@checked ? Expression.ConvertChecked(operand, conversionType) : Expression.Convert(operand, conversionType) , parameter);
			}
			catch(ArgumentException e)
			{
				WriteLine(e);
				return null;
			}
			catch(InvalidOperationException)
			{
				//do not walk through inheritance hierarchy for value types
				if(parameter.Type.IsValueType) return null;
				var lookup = operand.Type.BaseType;
				return lookup is null ? null : Convert(parameter, Expression.Convert(parameter, lookup), conversionType, @checked);
			}
		}

		private protected static Expression<D> MakeConvert<T>(ParameterExpression parameter, bool @checked)
			=> Convert(parameter, parameter, typeof(T), @checked);
	}
}
