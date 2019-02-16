using System;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Provides utility methods to work with collections.
    /// </summary>
    public static class Collection
    {
        public static ReadOnlyCollectionView<T> AsReadOnlyView<T>(this ICollection<T> collection)
            => new ReadOnlyCollectionView<T>(collection);

        public static ReadOnlyCollectionView<I, O> Convert<I, O>(this IReadOnlyCollection<I> collection, Converter<I, O> converter)
            => new ReadOnlyCollectionView<I, O>(collection, converter);

        private static T[] ToArray<C, T>(C collection, int count)
            where C: IEnumerable<T>
        {
            var result = new T[count];
            var index = 0L;
            foreach (var item in collection)
                result[index++] = item;
            return result;
        }

        public static T[] ToArray<T>(ICollection<T> collection) => ToArray<ICollection<T>, T>(collection, collection.Count);

        public static T[] ToArray<T>(IReadOnlyCollection<T> collection) => ToArray<IReadOnlyCollection<T>, T>(collection, collection.Count);

        public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> items)
            => items.ForEach(collection.Add);
    }
}