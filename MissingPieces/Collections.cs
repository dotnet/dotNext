using System.Collections.Generic;

namespace MissingPieces
{
    public static class Collections
    {
        public static string ToString<T>(this IEnumerable<T> collection, string delimiter)
            => string.Join(delimiter, collection);
    }
}