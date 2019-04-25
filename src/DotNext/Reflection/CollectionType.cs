using System;
using System.Collections.Generic;
using IEnumerable = System.Collections.IEnumerable;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides specialized reflection methods for
    /// collection types.
    /// </summary>
    public static class CollectionType
    {
        /// <summary>
        /// Obtains type of items in the collection type.
        /// </summary>
        /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
        /// <param name="enumerableInterface">The type <see cref="IEnumerable{T}"/> with actual generic argument.</param>
        /// <returns>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</returns>
        public static Type GetItemType(this Type collectionType, out Type enumerableInterface)
        {
            enumerableInterface = collectionType.FindGenericInstance(typeof(IEnumerable<>));
            if (!(enumerableInterface is null))
                return enumerableInterface.GetGenericArguments()[0];
            else if (typeof(IEnumerable).IsAssignableFrom(collectionType))
            {
                enumerableInterface = typeof(IEnumerable);
                return typeof(object);
            }
            else
            {
                enumerableInterface = null;
                return null;
            }
        }

        /// <summary>
        /// Obtains type of items in the collection type.
        /// </summary>
        /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
        /// <returns>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</returns>
        public static Type GetItemType(this Type collectionType)
            => collectionType.GetItemType(out _);
    }
}