using System.Reflection;

namespace DotNext.Reflection;

public static partial class Type<T>
{
    /// <summary>
    /// Provides typed access to the type attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of attribute.</typeparam>
    public static class Attribute<TAttribute>
        where TAttribute : Attribute
    {
        /// <summary>
        /// Returns attribute associated with the type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="inherit">True to find inherited attribute.</param>
        /// <param name="condition">Optional predicate to check attribute properties.</param>
        /// <returns>Attribute associated with type <typeparamref name="T"/>; or null, if attribute doesn't exist.</returns>
        public static TAttribute? Get(bool inherit = false, Predicate<TAttribute>? condition = null)
        {
            var attr = RuntimeType.GetCustomAttribute<TAttribute>(inherit);
            return attr is null || condition is null || condition(attr) ? attr : null;
        }

        /// <summary>
        /// Returns attribute associated with the type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="inherit">True to find inherited attribute.</param>
        /// <param name="condition">Optional predicate to check attribute properties.</param>
        /// <returns>Attribute associated with type <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingAttributeException">Event doesn't exist.</exception>
        public static TAttribute Require(bool inherit = false, Predicate<TAttribute>? condition = null)
            => Get(inherit, condition) ?? throw MissingAttributeException.Create<T, TAttribute>();

        /// <summary>
        /// Get all custom attributes of type <typeparamref name="TAttribute"/>.
        /// </summary>
        /// <param name="inherit">True to find inherited attribute.</param>
        /// <param name="condition">Optional predicate to check attribute properties.</param>
        /// <returns>All attributes associated with type <typeparamref name="T"/>.</returns>
        public static IEnumerable<TAttribute> GetAll(bool inherit = false, Predicate<TAttribute>? condition = null)
            => from attr in RuntimeType.GetCustomAttributes<TAttribute>(inherit)
               where condition is null || condition(attr)
               select attr;
    }
}