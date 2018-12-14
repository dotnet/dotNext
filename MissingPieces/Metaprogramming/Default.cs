namespace MissingPieces.Metaprogramming
{
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
	}
}
