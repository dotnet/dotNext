using System;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Provides extension methods for tuples.
    /// </summary>
    public static class TupleExtensions
    {
        /// <summary>
        /// Copies tuple items to an array.
        /// </summary>
        /// <typeparam name="T">The type of the tuple.</typeparam>
        /// <param name="tuple">The tuple instance.</param>
        /// <returns>An array of tuple items.</returns>
        public static object?[] ToArray<T>(this T tuple)
            where T : notnull, ITuple
        {
            object?[] result;
            if (tuple.Length > 0)
            {
                result = new object?[tuple.Length];
                for (var i = 0; i < result.Length; i++)
                    result[i] = tuple[i];
            }
            else
            {
                result = Array.Empty<object?>();
            }

            return result;
        }
    }
}