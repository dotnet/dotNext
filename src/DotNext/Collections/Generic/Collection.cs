using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

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

    /// <summary>
    /// Converts collection into single-dimensional array.
    /// </summary>
    /// <typeparam name="T">Type of collection items.</typeparam>
    /// <param name="collection">A collection to convert.</param>
    /// <returns>Array of collection items.</returns>
    public static T[] ToArray<T>(ICollection<T> collection)
    {
        var count = collection.Count;
        if (count == 0)
            return Array.Empty<T>();

        var result = GC.AllocateUninitializedArray<T>(count);
        collection.CopyTo(result, 0);
        return result;
    }

    /// <summary>
    /// Converts read-only collection into single-dimensional array.
    /// </summary>
    /// <typeparam name="T">Type of collection items.</typeparam>
    /// <param name="collection">A collection to convert.</param>
    /// <returns>Array of collection items.</returns>
    public static T[] ToArray<T>(IReadOnlyCollection<T> collection)
    {
        var count = collection.Count;
        if (count == 0)
            return Array.Empty<T>();

        var result = GC.AllocateUninitializedArray<T>(count);
        nuint index = 0;

        foreach (var item in collection)
            result[index++] = item;

        return result;
    }

    /// <summary>
    /// Adds multiple items into collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to modify.</param>
    /// <param name="items">An items to add into collection.</param>
    public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        switch (collection)
        {
            case null:
                throw new ArgumentNullException(nameof(collection));
            case List<T> list:
                list.AddRange(items);
                break;
            case HashSet<T> set:
                set.UnionWith(items);
                break;
            default:
                items.ForEach(collection.Add);
                break;
        }
    }
}