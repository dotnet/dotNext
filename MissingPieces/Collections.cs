using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MissingPieces
{
    public static class Collections
    {
        public static string ToString<T>(this IEnumerable<T> collection, string delimiter)
            => string.Join(delimiter, collection);

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
    }
}