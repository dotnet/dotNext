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
        internal const string ItemIndexerName = "Item";

        /// <summary>
        /// Obtains type of items in the collection type.
        /// </summary>
        /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
        /// <param name="enumerableInterface">The type <see cref="IEnumerable{T}"/> with actual generic argument.</param>
        /// <returns>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</returns>
        public static Type? GetItemType(this Type collectionType, out Type? enumerableInterface)
        {
            enumerableInterface = collectionType.FindGenericInstance(typeof(IEnumerable<>));
            if (!(enumerableInterface is null))
                return enumerableInterface.GetGenericArguments()[0];

            if (typeof(IEnumerable).IsAssignableFrom(collectionType))
            {
                enumerableInterface = typeof(IEnumerable);
                return typeof(object);
            }

            // handle async enumerable type
            enumerableInterface = collectionType.FindGenericInstance(typeof(IAsyncEnumerable<>));
            if (!(enumerableInterface is null))
                return enumerableInterface.GetGenericArguments()[0];

            enumerableInterface = null;
            return null;
        }

        /// <summary>
        /// Obtains type of items in the collection type.
        /// </summary>
        /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
        /// <returns>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</returns>
        public static Type? GetItemType(this Type collectionType)
            => collectionType.GetItemType(out _);

        /// <summary>
        /// Returns type of collection implemented by the given type.
        /// </summary>
        /// <remarks>
        /// The supported collection types are <see cref="ICollection{T}"/>, <seealso cref="IReadOnlyCollection{T}"/>.
        /// </remarks>
        /// <param name="type">The type that implements the one of the supported collection types.</param>
        /// <returns>The interface of the collection implemented by the given type; otherwise, <see langword="null"/> if collection interface is not implemented.</returns>
        /// <seealso cref="ICollection{T}"/>
        /// <seealso cref="IReadOnlyCollection{T}"/>
        public static Type? GetImplementedCollection(this Type type)
        {
            foreach (var collectionType in (typeof(IReadOnlyCollection<>), typeof(ICollection<>)).AsReadOnlySpan())
            {
                var instance = type.FindGenericInstance(collectionType);
                if (!(instance is null))
                    return instance;
            }

            return null;
        }
    }
}