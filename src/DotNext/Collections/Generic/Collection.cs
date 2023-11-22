using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Provides utility methods to work with collections.
/// </summary>
public static partial class Collection
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

    /// <summary>
    /// Computes hash code for the sequence of objects.
    /// </summary>
    /// <typeparam name="T">Type of the elements in the sequence.</typeparam>
    /// <param name="sequence">The sequence of elements.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>The hash code computed from each element in the sequence.</returns>
    public static int SequenceHashCode<T>(this IEnumerable<T> sequence, bool salted = true)
    {
        const int hashSalt = -1521134295;
        var hashCode = sequence.Aggregate(-910176598, static (hash, obj) => (hash * hashSalt) + (obj is null ? 0 : EqualityComparer<T>.Default.GetHashCode(obj)));
        return salted ? (hashCode * hashSalt) + RandomExtensions.BitwiseHashSalt : hashCode;
    }

    internal static bool SequenceEqual<T>(IEnumerable<T>? first, IEnumerable<T>? second)
        => first is null ? second is null : second is not null && Enumerable.SequenceEqual(first, second);

    /// <summary>
    /// Applies specified action to each collection element.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
    /// <param name="action">An action to applied for each element.</param>
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        switch (collection)
        {
            case List<T> list:
                Span.ForEach(CollectionsMarshal.AsSpan(list), action);
                break;
            case T[] array:
                Array.ForEach(array, action);
                break;
            case ArraySegment<T> segment:
                Span.ForEach(segment.AsSpan(), action);
                break;
            case LinkedList<T> list:
                ForEachNode(list, action);
                break;
            case string str:
                Span.ForEach(str.AsSpan(), Unsafe.As<Action<char>>(action));
                break;
            default:
                ForEachSlow(collection, action);
                break;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ForEachSlow(IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection)
                action(item);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ForEachNode(LinkedList<T> list, Action<T> action)
        {
            for (var node = list.First; node is not null; node = node.Next)
                action(node.Value);
        }
    }

    /// <summary>
    /// Applies the specified asynchronous action to each collection element.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
    /// <param name="action">An action to applied for each element.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
    public static async ValueTask ForEachAsync<T>(this IEnumerable<T> collection, Func<T, CancellationToken, ValueTask> action, CancellationToken token = default)
    {
        foreach (var item in collection)
            await action.Invoke(item, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtains element at the specified index in the sequence.
    /// </summary>
    /// <remarks>
    /// This method is optimized for types <see cref="IList{T}"/>
    /// and <see cref="IReadOnlyList{T}"/>.
    /// </remarks>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="collection">Source collection.</param>
    /// <param name="index">Index of the element to read.</param>
    /// <param name="element">Obtained element.</param>
    /// <returns><see langword="true"/>, if element is available in the collection and obtained successfully; otherwise, <see langword="false"/>.</returns>
    public static bool ElementAt<T>(this IEnumerable<T> collection, int index, [MaybeNullWhen(false)] out T element)
    {
        return collection switch
        {
            List<T> list => Span.ElementAt<T>(CollectionsMarshal.AsSpan(list), index, out element),
            T[] array => Span.ElementAt<T>(array, index, out element),
            LinkedList<T> list => NodeValueAt(list, index, out element),
            IList<T> list => ListElementAt(list, index, out element),
            IReadOnlyList<T> readOnlyList => ReadOnlyListElementAt(readOnlyList, index, out element),
            _ => ElementAtSlow(collection, index, out element),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool NodeValueAt(LinkedList<T> list, int matchIndex, [MaybeNullWhen(false)] out T element)
        {
            // slow but no memory allocation
            var index = 0;
            for (var node = list.First; node is not null; node = node.Next)
            {
                if (index++ == matchIndex)
                {
                    element = node.Value;
                    return true;
                }
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ElementAtSlow(IEnumerable<T> collection, int index, [MaybeNullWhen(false)] out T element)
        {
            using var enumerator = collection.GetEnumerator();
            enumerator.Skip(index);
            if (enumerator.MoveNext())
            {
                element = enumerator.Current;
                return true;
            }

            element = default!;
            return false;
        }

        static bool ListElementAt(IList<T> list, int index, [MaybeNullWhen(false)] out T element)
        {
            if ((uint)index < (uint)list.Count)
            {
                element = list[index];
                return true;
            }

            element = default!;
            return false;
        }

        static bool ReadOnlyListElementAt(IReadOnlyList<T> list, int index, [MaybeNullWhen(false)] out T element)
        {
            if ((uint)index < (uint)list.Count)
            {
                element = list[index];
                return true;
            }

            element = default!;
            return false;
        }
    }

    /// <summary>
    /// Skip <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T?> collection)
        where T : class
        => new NotNullEnumerable<T>(collection);

    /// <summary>
    /// Concatenates each element from the collection into single string.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <param name="collection">Collection to convert. Cannot be <see langword="null"/>.</param>
    /// <param name="delimiter">Delimiter between elements in the final string.</param>
    /// <param name="ifEmpty">A string to be returned if collection has no elements.</param>
    /// <returns>Converted collection into string.</returns>
    public static string ToString<T>(this IEnumerable<T> collection, string delimiter, string ifEmpty = "")
        => string.Join(delimiter, collection) is { Length: > 0 } result ? result : ifEmpty;

    /// <summary>
    /// Adds <paramref name="items"/> to the beginning of <paramref name="collection"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to be concatenated with the items.</param>
    /// <param name="items">The items to be added to the beginning of the collection.</param>
    /// <returns>The concatenated collection.</returns>
    public static IEnumerable<T> Prepend<T>(this IEnumerable<T> collection, params T[] items)
        => items.Concat(collection);

    /// <summary>
    /// Adds <paramref name="items"/> to the end of <paramref name="collection"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to be concatenated with the items.</param>
    /// <param name="items">The items to be added to the end of the collection.</param>
    /// <returns>The concatenated collection.</returns>
    public static IEnumerable<T> Append<T>(this IEnumerable<T> collection, params T[] items)
        => collection.Concat(items);

    /// <summary>
    /// Converts synchronous collection of elements to asynchronous.
    /// </summary>
    /// <param name="enumerable">The collection of elements.</param>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>The asynchronous wrapper over synchronous collection of elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is <see langword="null"/>.</exception>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        => new AsyncEnumerable.Proxy<T>(enumerable ?? throw new ArgumentNullException(nameof(enumerable)));
}