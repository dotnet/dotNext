using System;
using System.Runtime.CompilerServices;

namespace MissingPieces.Metaprogramming
{
	using static System.Linq.Expressions.Expression;

	/// <summary>
	/// Provides access to default value of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">Target type.</typeparam>
	public static class Default<T>
	{
		/// <summary>
		/// Default value.
		/// </summary>
		public static readonly T Value = default;

		internal static readonly System.Linq.Expressions.DefaultExpression Expression = System.Linq.Expressions.Expression.Default(typeof(T));

		private delegate bool IsDefaultPredicate(in T value);
		private static readonly IsDefaultPredicate isDefault;

		static Default()
		{
			var parameter = Parameter(typeof(T).MakeByRefType());
			if(parameter.Type.IsValueType)
			{
				var bitwiseEquality = typeof(StackValue<>).MakeGenericType(parameter.Type).GetMethod(nameof(StackValue<int>.BitwiseEquals));
				isDefault = Lambda<IsDefaultPredicate>(Call(null, bitwiseEquality, parameter, Expression), parameter).Compile();
			}
			else
				isDefault = Lambda<IsDefaultPredicate>(ReferenceEqual(parameter, Expression), parameter).Compile();
		}

		/// <summary>
		/// Checks whether the specified value is default value.
		/// </summary>
		/// <param name="value">Value to check.</param>
		/// <returns>True, if specified value is default value; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Is(in T value) => isDefault(in value);
	}
}
