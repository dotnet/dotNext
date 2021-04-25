using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext
{
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Various extension methods for reference types.
    /// </summary>
    public static class ObjectExtensions
    {
        internal static bool IsNull(object? obj) => obj is null;

        internal static bool IsNotNull(object? obj) => obj is not null;

        /// <summary>
        /// Provides ad-hoc approach to associate some data with the object
        /// without modification of it.
        /// </summary>
        /// <remarks>
        /// This method allows to associate arbitrary user data with any object.
        /// User data storage is not a part of object type declaration.
        /// Modification of user data doesn't cause modification of internal state of the object.
        /// The storage is associated with the object reference.
        /// Any user data are transient and can't be passed across process boundaries (i.e. serialization is not supported).
        /// </remarks>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">Target object.</param>
        /// <returns>User data storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UserDataStorage GetUserData<T>(this T obj)
            where T : class
            => new (obj);

        /// <summary>
        /// Checks whether the specified object is equal to one
        /// of the specified objects.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="object.Equals(object, object)"/>
        /// to check equality between two objects.
        /// </remarks>
        /// <typeparam name="T">The type of object to compare.</typeparam>
        /// <param name="value">The object to compare with other.</param>
        /// <param name="values">Candidate objects.</param>
        /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="values"/>.</returns>
        public static bool IsOneOf<T>(this T value, IEnumerable<T?> values)
            where T : class
        {
            foreach (var v in values)
            {
                if (Equals(value, v))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the specified object is equal to one
        /// of the specified objects.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="object.Equals(object, object)"/>
        /// to check equality between two objects.
        /// </remarks>
        /// <typeparam name="T">The type of object to compare.</typeparam>
        /// <param name="value">The object to compare with other.</param>
        /// <param name="values">Candidate objects.</param>
        /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="values"/>.</returns>
        public static bool IsOneOf<T>(this T value, params T?[] values)
            where T : class
        {
            for (nint i = 0; i < Intrinsics.GetLength(values); i++)
            {
                if (Equals(values[i], value))
                    return true;
            }

            return false;
        }

        internal static bool IsContravariant(object? obj, Type type) => obj?.GetType().IsAssignableFrom(type) ?? false;

        /// <summary>
        /// Reinterprets object reference.
        /// </summary>
        /// <remarks>
        /// This method can be used to get access to the explicitly implemented
        /// interface methods.
        /// </remarks>
        /// <example>
        /// <code>
        /// MemoryManager&lt;T&gt; manager;
        /// manager.As&lt;IDisposable&gt;().Dispose();
        /// </code>
        /// </example>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="obj">The object reference to reinterpret.</param>
        /// <returns>The reinterpreted <paramref name="obj"/> reference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T As<T>(this T obj)
            where T : class
            => obj;
    }
}
