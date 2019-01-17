using System;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    public static class Collections
    {
        public static ReadOnlyCollectionView<T> AsReadOnlyView<T>(this ICollection<T> collection)
            => new ReadOnlyCollectionView<T>(collection);

        public static ReadOnlyCollectionView<I, O> Convert<I, O>(this IReadOnlyCollection<I> collection, Converter<I, O> converter)
            => new ReadOnlyCollectionView<I, O>(collection, converter);
    }
}