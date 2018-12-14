using System;
using static System.Linq.Expressions.Expression;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Provides typed access to class or value type metadata.
	/// </summary>
	public static class Type<T>
	{
		/// <summary>
		/// Represents constructor without parameters.
		/// </summary>
		private static class Ctor
		{
			internal static readonly Func<T> Implementation;

			static Ctor()
			{
				var targetType = typeof(T);
				if (targetType.IsValueType)
					Implementation = Lambda<Func<T>>(Default<T>.Expression).Compile();
				else
				{
					var ctor = targetType.GetConstructor(EmptyArray<Type>.Value);
					Implementation = ctor == null ? null : Lambda<Func<T>>(New(ctor)).Compile();
				}
			}
		}

		/// <summary>
		/// Represents constructor with single parameter.
		/// </summary>
		/// <typeparam name="P">Type of constructor parameter.</typeparam>
		private static class Ctor<P>
		{
			internal static readonly Func<P, T> Implementation;

			static Ctor()
			{
				var ctor = typeof(T).GetConstructor(new[] { typeof(P) });
				if (ctor != null)
				{
					var parameter = Parameter(typeof(P));
					Implementation = Lambda<Func<P, T>>(New(ctor, parameter), parameter).Compile();
				}
			}
		}

		/// <summary>
		/// Represents constructor with two parameters.
		/// </summary>
		/// <typeparam name="P1">Type of constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of constructor parameter.</typeparam>
		private static class Ctor<P1, P2>
		{
			internal static readonly Func<P1, P2, T> Implementation;

			static Ctor()
			{
				var ctor = typeof(T).GetConstructor(new[] { typeof(P1), typeof(P2) });
				if (ctor != null)
				{
					var parameter1 = Parameter(typeof(P1));
					var parameter2 = Parameter(typeof(P2));
					Implementation = Lambda<Func<P1, P2, T>>(New(ctor, parameter1, parameter2), parameter1, parameter2).Compile();
				}
			}
		}

		/// <summary>
		/// Returns public constructor of type <typeparamref name="T"/> without parameters.
		/// </summary>
		/// <returns>A delegate representing public constructor without parameters.</returns>
		/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
		public static Func<T> Constructor()
			=> Ctor.Implementation ?? throw MissingConstructorException.Create<T>();

		/// <summary>
		/// Returns public constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="P"/>.
		/// </summary>
		/// <typeparam name="P">Type of constructor parameter.</typeparam>
		/// <returns>A delegate representing public constructor with single parameter.</returns>
		public static Func<P, T> Constructor<P>()
			=> Ctor<P>.Implementation ?? throw MissingConstructorException.Create<T, P>();

		/// <summary>
		/// Returns public constructor <typeparamref name="T"/> with two 
		/// parameters of type <typeparamref name="P1"/> and <typeparamref name="P2"/>.
		/// </summary>
		/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <returns>A delegate representing public constructor with two parameters.</returns>
		public static Func<P1, P2, T> Constructor<P1, P2>()
			=> Ctor<P1, P2>.Implementation ?? throw MissingConstructorException.Create<T, P1, P2>();

		/// <summary>
		/// Returns public instance property of type <typeparamref name="P"/>.
		/// </summary>
		/// <typeparam name="P">Type of property.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>Instance property.</returns>
		public static InstanceProperty<T, P> InstanceProperty<P>(string propertyName)
			=> InstanceProperty<T, P>.Get(propertyName);

		/// <summary>
		/// Returns public static property of type <typeparamref name="P"/>.
		/// </summary>
		/// <typeparam name="P">Type of property.</typeparam>
		/// <param name="propertyName">Property name.</param>
		/// <returns>Static property.</returns>
		public static StaticProperty<T, P> StaticProperty<P>(string propertyName)
			=> StaticProperty<T, P>.Get(propertyName);

		/// <summary>
		/// Returns public instance event with event handler of type <typeparamref name="E"/>.
		/// </summary>
		/// <typeparam name="E">Type of event handler.</typeparam>
		/// <param name="eventName">Event name.</param>
		/// <returns>Instance event.</returns>
		public static InstanceEvent<T, E> InstanceEvent<E>(string eventName)
			where E : MulticastDelegate
			=> InstanceEvent<T, E>.Get(eventName);

		/// <summary>
		/// Returns public static event with event handler of type <typeparamref name="E"/>.
		/// </summary>
		/// <typeparam name="E">Type of event handler.</typeparam>
		/// <param name="eventName">Event name.</param>
		/// <returns>Static event.</returns>
		public static StaticEvent<T, E> StaticEvent<E>(string eventName)
			where E : MulticastDelegate
			=> StaticEvent<T, E>.Get(eventName);
	}
}
