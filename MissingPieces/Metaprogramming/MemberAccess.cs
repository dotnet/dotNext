using System;
using System.Linq.Expressions;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Represents accessing instance field or property.
	/// </summary>
	/// <typeparam name="T">Declaring type.</typeparam>
	/// <typeparam name="V">Type of field or property value.</typeparam>
	/// <param name="instance">An object with property.</param>
	/// <param name="value">A value to set or get.</param>
	/// <param name="action">Action to be performed on the underlying member.</param>
	/// <returns>True, if action is supported by member; otherwise, false.</returns>
	public delegate bool MemberAccess<T, V>(in T instance, ref V value, MemberAction action);

	/// <summary>
	/// Represents accessing static field or property.
	/// </summary>
	/// <typeparam name="V">Type of field or property value.</typeparam>
	/// <param name="value">A value to set or get.</param>
	/// <param name="action">Actopm to be performed on the underlying member.</param>
	/// <returns>True, if action is supported by member; otherwise, false.</returns>
	public delegate bool MemberAccess<V>(ref V value, MemberAction action);

	/// <summary>
	/// Various extension methods for accessing member value.
	/// </summary>
	public static class MemberAccess
	{
		public delegate V Reader<T, out V>(in T instance);
		public delegate V Reader<out V>();
		public delegate void Writer<T, in V>(in T instance, V value);
		public delegate void Writer<in V>(V value);

		private static readonly ConstantExpression GetValueConst = Expression.Constant(MemberAction.GetValue, typeof(MemberAction));

		internal static ConditionalExpression GetOrSetValue(Expression action, Expression getValue, Expression setValue)
		{
			getValue = getValue is null ?
				Expression.Constant(false).Upcast<Expression, ConstantExpression>() :
				Expression.Block(getValue, Expression.Constant(true));
			setValue = setValue is null ?
				Expression.Constant(false).Upcast<Expression, ConstantExpression>() :
				Expression.Block(setValue, Expression.Constant(true));
			return Expression.Condition(Expression.Equal(action, GetValueConst), getValue, setValue);
		}

		private static MemberAccessException CannotChangeValue() => throw new MemberAccessException("Member cannot be modified");
		private static MemberAccessException CannotReadValue() => throw new MemberAccessException("Member value cannot be obtained");

		public static bool TrySetValue<T, V>(this MemberAccess<T, V> accessor, in T instance, V value)
			=> accessor(in instance, ref value, MemberAction.SetValue);

		public static void SetValue<T, V>(this MemberAccess<T, V> accessor, in T instance, V value)
		{
			if (!accessor.TrySetValue(in instance, value))
				throw CannotChangeValue();
		}

		public static bool TryGetValue<T, V>(this MemberAccess<T, V> accessor, in T instance, out V result)
		{
			result = default;
			return accessor(in instance, ref result, MemberAction.GetValue);
		}

		public static Optional<V> TryGetValue<T, V>(this MemberAccess<T, V> accessor, in T instance)
			=> accessor.TryGetValue(in instance, out var result) ? result : Optional<V>.Empty;

		public static V GetValue<T, V>(this MemberAccess<T, V> accessor, in T instance)
			=> accessor.TryGetValue(in instance, out var result) ? result : throw CannotReadValue();

		public static bool TrySetValue<V>(this MemberAccess<V> accessor, V value)
			=> accessor(ref value, MemberAction.SetValue);

		public static void SetValue<V>(this MemberAccess<V> accessor, V value)
		{
			if (!accessor.TrySetValue(value))
				throw CannotChangeValue();
		}

		public static bool TryGetValue<V>(this MemberAccess<V> accessor, out V result)
		{
			result = default;
			return accessor(ref result, MemberAction.GetValue);
		}

		public static Optional<V> TryGetValue<V>(this MemberAccess<V> accessor)
			=> accessor.TryGetValue(out var result) ? result : Optional<V>.Empty;

		public static V GetValue<V>(this MemberAccess<V> accessor)
			=> accessor.TryGetValue(out var result) ? result : throw CannotReadValue();

		public static MemberAccess<V> CaptureInstance<T, V>(this MemberAccess<T, V> accessor, T instance)
			=> delegate (ref V value, MemberAction action)
			{
				return accessor(in instance, ref value, action);
			};

		public static Reader<T, V> ToReader<T, V>(this MemberAccess<T, V> accessor)
			=> new Reader<T, V>(accessor.GetValue);

		public static Reader<V> ToReader<V>(this MemberAccess<V> accessor)
			=> new Reader<V>(accessor.GetValue);

		public static Writer<T, V> ToWriter<T, V>(this MemberAccess<T, V> accessor)
			=> new Writer<T, V>(accessor.SetValue);

		public static Writer<V> ToWriter<V>(this MemberAccess<V> accessor)
			=> new Writer<V>(accessor.SetValue);
	}
}
