using System;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Provides utility methods to work with collections.
    /// </summary>
    public static class Collection
    {
        /// <summary>
        /// Returns lazily converted read-only collection.
        /// </summary>
        /// <typeparam name="I">Type of items in the source collection.</typeparam>
        /// <typeparam name="O">Type of items in the target collection.</typeparam>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="converter">A collection item conversion function.</param>
        /// <returns>Lazily converted read-only collection.</returns>
        public static ReadOnlyCollectionView<I, O> Convert<I, O>(this IReadOnlyCollection<I> collection, in ValueFunc<I, O> converter)
            => new ReadOnlyCollectionView<I, O>(collection, converter);

        /// <summary>
        /// Returns lazily converted read-only collection.
        /// </summary>
        /// <typeparam name="I">Type of items in the source collection.</typeparam>
        /// <typeparam name="O">Type of items in the target collection.</typeparam>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="converter">A collection item conversion function.</param>
        /// <returns>Lazily converted read-only collection.</returns>
        public static ReadOnlyCollectionView<I, O> Convert<I, O>(this IReadOnlyCollection<I> collection, Converter<I, O> converter)
            => Convert(collection, converter.AsValueFunc(true));

        private static T[] ToArray<C, T>(C collection, int count)
            where C : class, IEnumerable<T>
        {
            var result = new T[count];
            var index = 0L;
            foreach (var item in collection)
                result[index++] = item;
            return result;
        }

        /// <summary>
        /// Converts collection into single-dimensional array.
        /// </summary>
        /// <typeparam name="T">Type of collection items.</typeparam>
        /// <param name="collection">A collection to convert.</param>
        /// <returns>Array of collection items.</returns>
        public static T[] ToArray<T>(ICollection<T> collection) => ToArray<ICollection<T>, T>(collection, collection.Count);

        /// <summary>
        /// Converts read-only collection into single-dimensional array.
        /// </summary>
        /// <typeparam name="T">Type of collection items.</typeparam>
        /// <param name="collection">A collection to convert.</param>
        /// <returns>Array of collection items.</returns>
        public static T[] ToArray<T>(IReadOnlyCollection<T> collection) => ToArray<IReadOnlyCollection<T>, T>(collection, collection.Count);

        /// <summary>
        /// Adds multiple items into collection.
        /// </summary>
        /// <typeparam name="T">Type of collection items.</typeparam>
        /// <param name="collection">A collection to modify.</param>
        /// <param name="items">An items to add into collection.</param>
        public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> items)
            => items.ForEach(collection.Add);
    }
}