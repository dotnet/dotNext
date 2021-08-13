using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
        /// <typeparam name="TInput">Type of items in the source collection.</typeparam>
        /// <typeparam name="TOutput">Type of items in the target collection.</typeparam>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="converter">A collection item conversion function.</param>
        /// <returns>Lazily converted read-only collection.</returns>
        public static ReadOnlyCollectionView<TInput, TOutput> Convert<TInput, TOutput>(this IReadOnlyCollection<TInput> collection, Converter<TInput, TOutput> converter)
            => new(collection, converter);

        private static T[] ToArray<TCollection, T>(TCollection collection, int count)
            where TCollection : class, IEnumerable<T>
        {
#if NETSTANDARD2_1
            var result = new T[count];
#else
            var result = GC.AllocateUninitializedArray<T>(count);
#endif
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
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to modify.</param>
        /// <param name="items">An items to add into collection.</param>
        public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> items)
            => items.ForEach(collection.Add);

        /// <summary>
        /// Gets the random element from the collection.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="collection">The collection to get the random element.</param>
        /// <param name="random">The random numbers source.</param>
        /// <returns>The random element from the collection; or <see cref="Optional{T}.None"/> if collection is empty.</returns>
        public static Optional<T> PeekRandom<T>(this IReadOnlyCollection<T> collection, Random random)
        {
            return collection.Count switch
            {
                0 => Optional<T>.None,
                1 => collection.FirstOrEmpty(),
                _ when collection is T[] array => Span.PeekRandom<T>(array, random),
#if !NETSTANDARD2_1
                _ when collection is List<T> list => Span.PeekRandom<T>(CollectionsMarshal.AsSpan(list), random),
#endif
                _ => PeekRandomSlow(collection, random),
            };

            static Optional<T> PeekRandomSlow(IReadOnlyCollection<T> collection, Random random)
            {
                var index = random.Next(collection.Count);
                using var enumerator = collection.GetEnumerator();
                for (var i = 0; enumerator.MoveNext(); i++)
                {
                    if (i == index)
                        return enumerator.Current;
                }

                return Optional<T>.None;
            }
        }
    }
}