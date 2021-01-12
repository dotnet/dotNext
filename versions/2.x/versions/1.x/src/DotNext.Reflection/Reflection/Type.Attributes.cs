using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides typed access to the type attribute.
        /// </summary>
        /// <typeparam name="A">Type of attribute.</typeparam>
        public static class Attribute<A>
            where A : Attribute
        {
            /// <summary>
            /// Returns attribute associated with the type <typeparamref name="T"/>.
            /// </summary>
            /// <param name="inherit">True to find inherited attribute.</param>
            /// <param name="condition">Optional predicate to check attribute properties.</param>
            /// <returns>Attribute associated with type <typeparamref name="T"/>; or null, if attribute doesn't exist.</returns>
            public static A Get(bool inherit = false, Predicate<A> condition = null)
            {
                var attr = RuntimeType.GetCustomAttribute<A>(inherit);
                return attr is null || condition is null || condition(attr) ? attr : null;
            }

            /// <summary>
            /// Returns attribute associated with the type <typeparamref name="T"/>.
            /// </summary>
            /// <param name="inherit">True to find inherited attribute.</param>
            /// <param name="condition">Optional predicate to check attribute properties.</param>
            /// <returns>Attribute associated with type <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingAttributeException">Event doesn't exist.</exception>
            public static A Require(bool inherit = false, Predicate<A> condition = null)
                => Get(inherit, condition) ?? throw MissingAttributeException.Create<T, A>();

            /// <summary>
            /// Get all custom attributes of type <typeparamref name="A"/>.
            /// </summary>
            /// <param name="inherit">True to find inherited attribute.</param>
            /// <param name="condition">Optional predicate to check attribute properties.</param>
            /// <returns>All attributes associated with type <typeparamref name="T"/>.</returns>
            public static IEnumerable<A> GetAll(bool inherit = false, Predicate<A> condition = null)
                => from attr in RuntimeType.GetCustomAttributes<A>(inherit)
                   where condition is null || condition(attr)
                   select attr;
        }
    }
}