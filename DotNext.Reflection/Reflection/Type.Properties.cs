using System.Reflection;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
		/// <summary>
		/// Provides access to property declared in type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="V">Type of property.</typeparam>
		public static class Property<V>
		{
			private sealed class InstanceProperties : MemberCache<PropertyInfo, Property<T, V>>
			{
				internal static readonly InstanceProperties Public = new InstanceProperties(false);
				internal static readonly InstanceProperties NonPublic = new InstanceProperties(true);

				private readonly bool nonPublic;
				private InstanceProperties(bool nonPublic) => this.nonPublic = nonPublic;

				private protected override Property<T, V> Create(string propertyName)
					=> Property<T, V>.Reflect(propertyName, nonPublic);
			}

			private sealed class StaticProperties : MemberCache<PropertyInfo, Reflection.Property<V>>
			{
				internal static readonly StaticProperties Public = new StaticProperties(false);
				internal static readonly StaticProperties NonPublic = new StaticProperties(true);

				private readonly bool nonPublic;
				private StaticProperties(bool nonPublic) => this.nonPublic = nonPublic;

				private protected override Reflection.Property<V> Create(string propertyName)
					=> Reflection.Property<V>.Reflect<T>(propertyName, nonPublic);
			}

			/// <summary>
			/// Gets instance property.
			/// </summary>
			/// <param name="propertyName">Name of property.</param>
			/// <param name="nonPublic">True to reflect non-public property.</param>
			/// <returns>Property field; or null, if property doesn't exist.</returns>
			public static Property<T, V> Get(string propertyName, bool nonPublic = false)
				=> (nonPublic ? InstanceProperties.NonPublic : InstanceProperties.Public).GetOrCreate(propertyName);

			/// <summary>
			/// Gets instance property.
			/// </summary>
			/// <param name="propertyName">Name of property.</param>
			/// <param name="nonPublic">True to reflect non-public property.</param>
			/// <returns>Instance property.</returns>
			/// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
			public static Property<T, V> Require(string propertyName, bool nonPublic = false)
				=> Get(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, V>(propertyName);

			/// <summary>
			/// Gets static property.
			/// </summary>
			/// <param name="propertyName">Name of property.</param>
			/// <param name="nonPublic">True to reflect non-public property.</param>
			/// <returns>Instance property; or null, if property doesn't exist.</returns>
			public static Reflection.Property<V> GetStatic(string propertyName, bool nonPublic = false)
				=> (nonPublic ? StaticProperties.NonPublic : StaticProperties.Public).GetOrCreate(propertyName);

			/// <summary>
			/// Gets static property.
			/// </summary>
			/// <param name="propertyName">Name of property.</param>
			/// <param name="nonPublic">True to reflect non-public property.</param>
			/// <returns>Instance property.</returns>
			/// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
			public static Reflection.Property<V> RequireStatic(string propertyName, bool nonPublic = false)
				=> GetStatic(propertyName, nonPublic) ?? throw MissingFieldException.Create<T, V>(propertyName);
		}
	}
}