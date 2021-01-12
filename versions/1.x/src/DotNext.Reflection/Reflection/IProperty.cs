using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected property.
    /// </summary>
    public interface IProperty : IMember<PropertyInfo>
    {
        /// <summary>
        /// Gets a value indicating whether the property can be read.
        /// </summary>
		bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether the property can be written to.
        /// </summary>
		bool CanWrite { get; }
    }

    /// <summary>
    /// Represents static property.
    /// </summary>
    /// <typeparam name="P">Type of property value.</typeparam>
    public interface IProperty<P> : IProperty
    {
        /// <summary>
        /// Gets or sets property value.
        /// </summary>
        P Value { get; set; }
    }

    /// <summary>
    /// Represents instance property.
    /// </summary>
    /// <typeparam name="T">Property declaring type.</typeparam>
    /// <typeparam name="P">Type of property value.</typeparam>
    public interface IProperty<T, P> : IProperty
    {
        /// <summary>
        /// Gets or sets property value.
        /// </summary>
        /// <param name="this">The object whose property value will be set or returned.</param>
        /// <returns>Property value.</returns>
        P this[in T @this] { get; set; }
    }
}
