using System.Runtime.CompilerServices;
using System.Reflection;
using System.Linq.Expressions;

namespace MissingPieces
{
	public static class ValueTypes
	{
		internal static MethodCallExpression BitwiseEqualsMethodCall(Expression first, Expression second)
		{
			var method = typeof(ValueTypes).GetMethod(nameof(BitwiseEquals), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			method = method.MakeGenericMethod(first.Type);
			return Expression.Call(null, method, first, second);
		}

		/// <summary>
		/// Performs bitwise equality between two structures.
		/// </summary>
		/// <param name="first">The first structure to compare.</param>
		/// <param name="second">The second structure to compare.</param>
		/// <typeparam name="T">Type of structure.</typeparam>
		/// <returns>True, if both structures have the same set of bits.</returns>
		public static bool BitwiseEquals<T>(in T first, in T second)
			where T: struct
			=> ((Ref<T>)first).BitwiseEquals((Ref<T>)second);
	}
}
