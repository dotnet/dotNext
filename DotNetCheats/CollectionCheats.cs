using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cheats
{
    public static class CollectionCheats
    {
        public static T? FirstOrNull<T>(this IEnumerable<T> collection)
            where T: struct
        {
            using(var enumerator = collection.GetEnumerator())
                return enumerator.MoveNext() ? enumerator.Current : new T?();
        }

        public static Optional<T> TryGetFirst<T>(this IEnumerable<T> collection)
        {
            using(var enumerator = collection.GetEnumerator())
                return enumerator.MoveNext() ? enumerator.Current : Optional<T>.Empty;
        }

        public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T> collection)
            where T: class
            => collection.Where(value => !(value is null));

        public static string ToString<T>(this IEnumerable<T> collection, string delimiter, string ifEmpty = "")
            => string.Join(delimiter, collection).IfNullOrEmpty(ifEmpty);

        public static O[] MapToArray<I, O>(this IList<I> input, Func<I, O> mapper)
        {
            var output = Arrays.New<O>(input.Count);
            for(var i = 0; i < input.Count; i++)
                output[i] = mapper(input[i]);
            return output;
        }

        public static O[] MapToArray<I, O>(this IList<I> input, Func<int, I, O> mapper)
        {
            var output = Arrays.New<O>(input.Count);
            for(var i = 0; i < input.Count; i++)
                output[i] = mapper(i, input[i]);
            return output;
        }

        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
            => collection is null || collection.Count == 0;

        public static C IfNullOrEmpty<C, T>(this C collection, C value)
            where C:ICollection<T>
            => IsNullOrEmpty(collection) ? value : collection;

    }
}