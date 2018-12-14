using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Provides typed access to static property.
	/// </summary>
	/// <typeparam name="T">Declaring type of property.</typeparam>
	/// <typeparam name="P">Property type.</typeparam>
	public readonly struct StaticProperty<T, P> : IProperty, IEquatable<StaticProperty<T, P>>, IEquatable<PropertyInfo>
	{
		private sealed class Cache : MemberCache<PropertyInfo, StaticProperty<T, P>>
		{
			private protected override StaticProperty<T, P> CreateMember(string propertyName)
				=> new StaticProperty<T, P>(propertyName);
		}

		private static readonly Cache properties = new Cache();
		private readonly Func<P> getter;
		private readonly Action<P> setter;
		private readonly PropertyInfo property;

		private StaticProperty(string name)
		{
			property = typeof(T).GetProperty(name, BindingFlags.Static | BindingFlags.Public);
			if (property is null || property.PropertyType != typeof(P))
			{
				getter = null;
				setter = null;
			}
			else
			{
				getter = property.GetMethod?.CreateDelegate<Func<P>>(null);
				setter = property.SetMethod?.CreateDelegate<Action<P>>(null);
			}
		}

		/// <summary>
		/// Generates property access expression.
		/// </summary>
		/// <returns>Property access expression.</returns>
		public MemberExpression CreateExpression()
			=> Expression.Property(null, property);

		/// <summary>
		/// Gets or sets property value.
		/// </summary>
		public P Value
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => getter();
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => setter(value);
		}

		/// <summary>
		/// Gets name of property.
		/// </summary>
		public string Name => property.Name;

		/// <summary>
		/// Indicates that property has getter method.
		/// </summary>
		public bool CanRead => getter != null;

		/// <summary>
		/// Indicates that property has setter method.
		/// </summary>
		public bool CanWrite => setter != null;

		/// <summary>
		/// Indicates that this object references property.
		/// </summary>
		public bool Exists => property != null;

		PropertyInfo IMember<PropertyInfo>.Member => property;

		public bool Equals(PropertyInfo other) => property == other;

		public bool Equals(in StaticProperty<T, P> other)
			=> Equals(other.property);

		bool IEquatable<StaticProperty<T, P>>.Equals(StaticProperty<T, P> other)
			=> Equals(in other);

		public override bool Equals(object other)
		{
			switch (other)
			{
				case StaticProperty<T, P> property:
					return Equals(in property);
				case PropertyInfo property:
					return Equals(property);
				default:
					return false;
			}
		}

		public override int GetHashCode() => property.GetHashCode();

		public override string ToString() => property.ToString();

		public static bool operator ==(in StaticProperty<T, P> first, in StaticProperty<T, P> second)
			=> first.Equals(in second);

		public static bool operator !=(in StaticProperty<T, P> first, in StaticProperty<T, P> second)
			=> !first.Equals(in second);

		public static implicit operator PropertyInfo(in StaticProperty<T, P> property)
			=> property.property;

		public static explicit operator P(in StaticProperty<T, P> property)
			=> property.Value;

		internal static StaticProperty<T, P> Get(string propertyName)
			=> properties.GetOrCreate(propertyName);
	}
}
