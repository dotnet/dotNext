using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Provides typed access to instance property.
	/// </summary>
	/// <remarks>Access to property is organized through managed reference
	/// to the instance object. Therefore, original struct value will be modified.
	/// </remarks>
	/// <typeparam name="T">Declaring type of property.</typeparam>
	/// <typeparam name="P">Property type.</typeparam>
	public readonly struct InstanceProperty<T, P> : IProperty, IEquatable<InstanceProperty<T, P>>, IEquatable<PropertyInfo>
	{
		private sealed class Cache : MemberCache<PropertyInfo, InstanceProperty<T, P>>
		{
			private protected override InstanceProperty<T, P> CreateMember(string propertyName)
				=> new InstanceProperty<T, P>(propertyName);
		}

		private delegate P PropertyGetter(in T instance);
		private delegate void PropertySetter(in T instance, P value);

		private static readonly Cache properties = new Cache();
		private readonly PropertyGetter getter;
		private readonly PropertySetter setter;
		private readonly PropertyInfo property;

		private InstanceProperty(string name)
		{
			property = typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if (property is null || property.PropertyType != typeof(P))
			{
				getter = null;
				setter = null;
			}
			else
			{
				var instanceParam = Expression.Parameter(property.DeclaringType.MakeByRefType());
				if (property.GetMethod is null)
					getter = null;
				else
					getter = Expression.Lambda<PropertyGetter>(Expression.Property(instanceParam, property), instanceParam).Compile();
				if (property.SetMethod is null)
					setter = null;
				else
				{
					var valueParam = Expression.Parameter(property.PropertyType);
					setter = Expression.Lambda<PropertySetter>(Expression.Assign(Expression.Property(instanceParam, property), valueParam), instanceParam, valueParam).Compile();
				}
			}
		}

		/// <summary>
		/// Generates property access expression.
		/// </summary>
		/// <param name="instance">Property instance.</param>
		/// <returns>Property access expression.</returns>
		public MemberExpression CreateExpression(Expression instance)
			=> Expression.Property(instance, property);

		/// <summary>
		/// Gets or sets property value.
		/// </summary>
		/// <param name="owner">Property instance.</param>
		/// <returns>Property value.</returns>
		public P this[in T owner]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => getter(in owner);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => setter(in owner, value);
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

		public bool Equals(in InstanceProperty<T, P> other)
			=> Equals(other.property);

		bool IEquatable<InstanceProperty<T, P>>.Equals(InstanceProperty<T, P> other)
			=> Equals(in other);

		public override bool Equals(object other)
		{
			switch (other)
			{
				case InstanceProperty<T, P> property:
					return Equals(in property);
				case PropertyInfo property:
					return Equals(property);
				default:
					return false;
			}
		}

		public override int GetHashCode() => property.GetHashCode();

		public override string ToString() => property.ToString();

		public static bool operator ==(in InstanceProperty<T, P> first, in InstanceProperty<T, P> second)
			=> first.Equals(in second);

		public static bool operator !=(in InstanceProperty<T, P> first, in InstanceProperty<T, P> second)
			=> !first.Equals(in second);

		public static implicit operator PropertyInfo(in InstanceProperty<T, P> property)
			=> property.property;

		internal static InstanceProperty<T, P> Get(string propertyName)
			=> properties.GetOrCreate(propertyName);
	}
}
