using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNetCheats
{
	/// <summary>
	/// Various extension and factory methods for constructing optional value.
	/// </summary>
	public static class Optional
	{
		private static bool IsOptional(Type optionalType)
			=> optionalType != null &&
				optionalType.IsGenericType &&
				!optionalType.IsGenericTypeDefinition &&
				optionalType.GetGenericTypeDefinition() == typeof(Optional<>);

		/// <summary>
		/// Returns the underlying type argument of the specified optional type.
		/// </summary>
		/// <param name="optionalType">Optional type.</param>
		/// <returns>Underlying type argument of optional type; otherwise, null.</returns>
		public static Type GetUnderlyingType(Type optionalType)
			=> IsOptional(optionalType) ?
				optionalType.GetGenericArguments()[0] :
				null;

		/// <summary>
		/// Indicates that specified type is optional type.
		/// </summary>
		/// <typeparam name="T">Type to check.</typeparam>
		/// <returns>True, if specified type is optional type; otherwise, false.</returns>
		public static bool IsOptional<T>() => IsOptional(typeof(T));

		/// <summary>
		/// Constructs optional value from nullable reference type.
		/// </summary>
		/// <typeparam name="T">Type of value.</typeparam>
		/// <param name="value">The value to convert.</param>
		/// <returns></returns>
		public static Optional<T> ToOptional<T>(this in T? value)
			where T : struct
			=> value ?? Optional<T>.Empty;

		public static Optional<T> EmptyIfNull<T>(this T value)
			where T: class
			=> value is null ? default : new Optional<T>(value);

		/// <summary>
		/// If a value is present, returns the value, otherwise null.
		/// </summary>
		/// <typeparam name="T">Value type.</typeparam>
		/// <param name="value">Optional value.</param>
		/// <returns>Nullable value.</returns>
		public static T? OrNull<T>(this in Optional<T> value)
			where T : struct
			=> value.IsPresent ? new T?(value.Value) : null;

		public static ref readonly Optional<T> Coalesce<T>(this in Optional<T> first, in Optional<T> second)
			=> ref first.IsPresent ? ref first : ref second;

		public static ref readonly Optional<T> Coalesce<T>(this in Optional<T> first, in Optional<T> second, in Optional<T> third)
		{
			if (first.IsPresent)
				return ref first;
			else if (second.IsPresent)
				return ref second;
			else
				return ref third;
		}

		public static ref readonly Optional<T> Coalesce<T>(this in Optional<T> first, in Optional<T> second, in Optional<T> third, in Optional<T> fourth)
		{
			if (first.IsPresent)
				return ref first;
			else if (second.IsPresent)
				return ref second;
			else if (third.IsPresent)
				return ref third;
			else
				return ref fourth;
		}

		private static PropertyInfo GetHasContentProperty(Type targetType)
		{
			var optionalInterface = typeof(IOptional);
			return optionalInterface.IsAssignableFrom(targetType) ?
				optionalInterface.GetProperty(nameof(IOptional.IsPresent)) :
				null;
			
		}

		private static Expression HasContentPropertyExpression(Expression input)
		{
			var property = GetHasContentProperty(input.Type);
			return property is null ? null : Expression.Property(input, property);
		}

		private static bool IsNothing(Type target)
			=> target == typeof(void) || target == typeof(ValueTuple);

		internal static Expression CheckerBodyForValueType(Expression input)
		{
			if (input.Type.OneOf(typeof(void), typeof(ValueTuple)))
				return Expression.Constant(false);
			var nullableType = Nullable.GetUnderlyingType(input.Type);
			if (nullableType is null)   //handle regular struct
				return HasContentPropertyExpression(input) ?? Expression.Constant(true);
			//handle nullable type
			var hasValuePropertyExpr = Expression.Property(input,
				input.Type.GetProperty(nameof(Nullable<int>.HasValue), typeof(bool)));
			var valuePropertyExpr = Expression.Property(input,
				input.Type.GetProperty(nameof(Nullable<int>.Value), nullableType));
			//recursive call to unwind nullable chain
			//input.HasValue && input.Value.HasContent -or- input.HasValue
			return Expression.AndAlso(hasValuePropertyExpr, CheckerBodyForValueType(valuePropertyExpr));
		}

		internal static Expression CheckerBodyForReferenceType(ParameterExpression input)
		{
			//HasContent property reference
			var hasContentPropertyExpr = HasContentPropertyExpression(input);
			return hasContentPropertyExpr is null ?
				 //input != null
				 Expression.ReferenceNotEqual(input, Expression.Constant(null, input.Type)) :
				 //input != null && input.HasContent
				 Expression.AndAlso(
					Expression.ReferenceNotEqual(input, Expression.Constant(null, input.Type)),
					hasContentPropertyExpr);
		}
	}

	/// <summary>
	/// A container object which may or may not contain a value.
	/// </summary>
	/// <typeparam name="T">Type of value.</typeparam>
	public readonly struct Optional<T> : IOptional, IEquatable<Optional<T>>, IEquatable<T>, IStructuralEquatable
	{
		private delegate bool ByRefPredicate(in T value);

		/// <summary>
		/// Highly optimized checker of the content.
		/// </summary>
		private static readonly ByRefPredicate HasValueChecker;

		static Optional()
		{
			//describes predicate parameter
			var parameter = Expression.Parameter(typeof(T).MakeByRefType());
			Expression checkerBody = parameter.Type.IsValueType ?
				Optional.CheckerBodyForValueType(parameter) :
				Optional.CheckerBodyForReferenceType(parameter);
			HasValueChecker = Expression.Lambda<ByRefPredicate>(checkerBody, parameter).Compile();
		}

		private readonly T value;
		private readonly bool isPresent;

		/// <summary>
		/// Constructs non-empty container.
		/// </summary>
		/// <param name="value">A value to be placed into container.</param>
		public Optional(T value)
		{
			this.value = value;
			isPresent = true;
		}

		/// <summary>
		/// Represents optional container without value.
		/// </summary>
		public static Optional<T> Empty = default;

		/// <summary>
		/// Indicates whether the value is present.
		/// </summary>
		public bool IsPresent => isPresent && HasValueChecker(in value);

		/// <summary>
		/// Indicates that specified value has meaningful content.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <returns>True, if value has meaningful content; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasValue(in T value) => HasValueChecker(in value);

		public bool TryGet(out T value)
		{
			if(IsPresent)
			{
				value = this.value;
				return true;
			}
			else
			{
				value = default;
				return false;
			}
		}

		/// <summary>
		/// Returns the value if present; otherwise return default value.
		/// </summary>
		/// <param name="defaultValue">The value to be returned if there is no value present.</param>
		/// <returns>The value, if present, otherwise default</returns>
		public T Or(T defaultValue) => IsPresent ? value : defaultValue;

		/// <summary>
		/// If a value is present, returns the value, otherwise throw exception.
		/// </summary>
		/// <typeparam name="E">Type of exception to throw.</typeparam>
		/// <returns>The value, if present.</returns>
		public T OrThrow<E>()
			where E : Exception, new()
			=> IsPresent ? value : throw new E();

		/// <summary>
		/// If a value is present, returns the value, otherwise throw exception.
		/// </summary>
		/// <typeparam name="E"></typeparam>
		/// <param name="exceptionFactory">Exception factory.</param>
		/// <returns>The value, if present.</returns>
		public T OrThrow<E>(Func<E> exceptionFactory)
			where E : Exception
			=> IsPresent ? value : throw exceptionFactory();

		/// <summary>
		/// Returns the value if present; otherwise invoke delegate.
		/// </summary>
		/// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
		/// <returns>The value, if present, otherwise returned from delegate.</returns>
		public T OrInvoke(Func<T> defaultFunc) => IsPresent ? value : defaultFunc();

		/// <summary>
		/// If a value is present, returns the value, otherwise return default value.
		/// </summary>
		/// <returns>The value, if present, otherwise default<</returns>
		public T OrDefault() => Or(default);

		/// <summary>
		/// If a value is present, returns the value, otherwise throw exception.
		/// </summary>
		/// <exception cref="InvalidOperationException">No value is present.</exception>
		public T Value => IsPresent ? value : throw new InvalidOperationException("Container has no value");

		/// <summary>
		/// If a value is present, apply the provided mapping function to it, and if the result is 
		/// non-null, return an Optional describing the result. Otherwise return an empty Optional.
		/// </summary>
		/// <typeparam name="U">The type of the result of the mapping function.</typeparam>
		/// <param name="mapper">A mapping function to apply to the value, if present.</param>
		/// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise an empty Optional.</returns>
		public Optional<U> Map<U>(Func<T, U> mapper) => IsPresent ? mapper(value) : Optional<U>.Empty;

		public Optional<U> FlatMap<U>(Func<T, Optional<U>> mapper) => IsPresent ? mapper(value) : Optional<U>.Empty;

		/// <summary>
		/// If a value is present, and the value matches the given predicate, 
		/// return an Optional describing the value, otherwise return an empty Optional.
		/// </summary>
		/// <param name="condition">A predicate to apply to the value, if present.</param>
		/// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional</returns>
		public Optional<T> If(Predicate<T> condition) => IsPresent && condition(value) ? this : Empty;

		public override string ToString() => IsPresent ? value.ToString() : "<EMPTY>";

		public override int GetHashCode() => IsPresent ? value.GetHashCode() : 0;

		public bool Equals(T other) => IsPresent && value.Equals(other);

		bool IEquatable<Optional<T>>.Equals(Optional<T> other)
			=> Equals(in other);

		[CLSCompliant(false)]
		public bool Equals(in Optional<T> other)
		{
			var present1 = IsPresent;
			var present2 = other.IsPresent;
			return present1 & present2 ? value.Equals(other.value) : present1 == present2;
		}

		public override bool Equals(object other)
		{
			switch (other)
			{
				case Optional<T> optional:
					return Equals(optional);
				case T value:
					return Equals(value);
				default:
					return false;
			}
		}

		public bool Equals(object other, IEqualityComparer comparer)
			=> other is T && IsPresent && comparer.Equals(value, other);

		public int GetHashCode(IEqualityComparer comparer)
			=> IsPresent ? comparer.GetHashCode(value) : 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Optional<T>(T value) => new Optional<T>(value);

		public static explicit operator T(in Optional<T> optional) => optional.Value;
		public static bool operator ==(in Optional<T> first, in Optional<T> second)
			=> first.Equals(in second);
		public static bool operator !=(in Optional<T> first, in Optional<T> second)
			=> !first.Equals(in second);

		public static Optional<T> operator |(in Optional<T> first, in Optional<T> second)
			=> first.IsPresent ? first : second;
	}
}