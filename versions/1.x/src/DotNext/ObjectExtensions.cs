using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Various extension methods for reference types.
    /// </summary>
    public static class ObjectExtensions
    {
        internal static bool IsNull(object obj) => obj is null;

        internal static bool IsNotNull(object obj) => !(obj is null);

        /// <summary>
        /// Provides ad-hoc approach to associate some data with the object
        /// without modification of it.
        /// </summary>
        /// <remarks>
        /// This method allows to associate arbitrary user data with any object.
        /// User data storage is not a part of object type declaration.
        /// Modification of user data doesn't cause modification of internal state of the object.
        /// The storage is associated with the object reference.
        /// Any user data are transient and can't be passed across process boundaries (i.e. serialization is not supported)
        /// </remarks>
        /// <param name="obj">Target object.</param>
        /// <returns>User data storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UserDataStorage GetUserData<T>(this T obj)
            where T : class
            => new UserDataStorage(obj);

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
        public static bool IsOneOf<T>(this T value, IEnumerable<T> values)
            where T : class
        {
            foreach (var v in values)
                if (Equals(value, v))
                    return true;
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
        public static bool IsOneOf<T>(this T value, params T[] values)
            where T : class
            => value.IsOneOf((IEnumerable<T>)values);

        /// <summary>
        /// Performs decomposition of object into two values.
        /// </summary>
        /// <typeparam name="T">Type of object to decompose.</typeparam>
        /// <typeparam name="R1">Type of the first decomposition result.</typeparam>
        /// <typeparam name="R2">Type of the second decomposition result.</typeparam>
        /// <param name="obj">An object to decompose.</param>
        /// <param name="decomposer1">First decomposition function.</param>
        /// <param name="decomposer2">Second decomposition function.</param>
        /// <param name="result1">First decomposition result.</param>
        /// <param name="result2">Second decomposition result.</param>
        public static void Decompose<T, R1, R2>(this T obj, Func<T, R1> decomposer1, Func<T, R2> decomposer2, out R1 result1, out R2 result2)
            where T : class
            => Decompose(obj, new ValueFunc<T, R1>(decomposer1, true), new ValueFunc<T, R2>(decomposer2, true), out result1, out result2);

        /// <summary>
        /// Performs decomposition of object into two values.
        /// </summary>
        /// <typeparam name="T">Type of object to decompose.</typeparam>
        /// <typeparam name="R1">Type of the first decomposition result.</typeparam>
        /// <typeparam name="R2">Type of the second decomposition result.</typeparam>
        /// <param name="obj">An object to decompose.</param>
        /// <param name="decomposer1">First decomposition function.</param>
        /// <param name="decomposer2">Second decomposition function.</param>
        /// <param name="result1">First decomposition result.</param>
        /// <param name="result2">Second decomposition result.</param>
        public static void Decompose<T, R1, R2>(this T obj, in ValueFunc<T, R1> decomposer1, in ValueFunc<T, R2> decomposer2, out R1 result1, out R2 result2)
            where T : class
        {
            result1 = decomposer1.Invoke(obj);
            result2 = decomposer2.Invoke(obj);
        }

        /// <summary>
        /// Performs decomposition of object into tuple.
        /// </summary>
        /// <typeparam name="T">Type of object to decompose.</typeparam>
        /// <typeparam name="R1">Type of the first decomposition result.</typeparam>
        /// <typeparam name="R2">Type of the second decomposition result.</typeparam>
        /// <param name="obj">An object to decompose.</param>
        /// <param name="decomposer1">First decomposition function.</param>
        /// <param name="decomposer2">Second decomposition function.</param>
        /// <returns>Decomposition result.</returns>
        public static (R1, R2) Decompose<T, R1, R2>(this T obj, Func<T, R1> decomposer1, Func<T, R2> decomposer2)
            where T : class
            => Decompose(obj, new ValueFunc<T, R1>(decomposer1, true), new ValueFunc<T, R2>(decomposer2, true));

        /// <summary>
        /// Performs decomposition of object into tuple.
        /// </summary>
        /// <typeparam name="T">Type of object to decompose.</typeparam>
        /// <typeparam name="R1">Type of the first decomposition result.</typeparam>
        /// <typeparam name="R2">Type of the second decomposition result.</typeparam>
        /// <param name="obj">An object to decompose.</param>
        /// <param name="decomposer1">First decomposition function.</param>
        /// <param name="decomposer2">Second decomposition function.</param>
        /// <returns>Decomposition result.</returns>
        public static (R1, R2) Decompose<T, R1, R2>(this T obj, in ValueFunc<T, R1> decomposer1, in ValueFunc<T, R2> decomposer2)
            where T : class
        {
            var tuple = default((R1 result1, R2 result2));
            obj.Decompose(decomposer1, decomposer2, out tuple.result1, out tuple.result2);
            return tuple;
        }

        internal static bool IsContravariant(object obj, Type type) => obj != null && obj.GetType().IsAssignableFrom(type);
    }
}
