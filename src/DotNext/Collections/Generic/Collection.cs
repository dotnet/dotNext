using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Provides utility methods to work with collections.
/// </summary>
public static partial class Collection
{
    /// <summary>
    /// Extends <see cref="ICollection{T}"/>.
    /// </summary>
    /// <param name="collection">A collection to modify.</param>
    /// <typeparam name="T">Type of collection items.</typeparam>
    extension<T>(ICollection<T> collection)
    {
        /// <summary>
        /// Converts collection into single-dimensional array.
        /// </summary>
        /// <param name="col">A collection to convert.</param>
        /// <returns>Array of collection items.</returns>
        public static T[] ToArray(ICollection<T> col)
        {
            T[] result;
            var count = col.Count;
            if (count is 0)
            {
                result = [];
            }
            else
            {
                result = GC.AllocateUninitializedArray<T>(count);
                col.CopyTo(result, 0);
            }

            return result;
        }
        
        /// <summary>
        /// Adds multiple items into collection.
        /// </summary>
        /// <param name="items">An items to add into collection.</param>
        public void AddAll(IEnumerable<T> items)
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

    /// <summary>
    /// Extends <see cref="IReadOnlyCollection{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type of collection items.</typeparam>
    /// <param name="collection">A collection to convert.</param>
    extension<T>(IReadOnlyCollection<T> collection)
    {
        /// <summary>
        /// Converts read-only collection into single-dimensional array.
        /// </summary>
        /// <typeparam name="T">Type of collection items.</typeparam>
        /// <param name="col">A collection to convert.</param>
        /// <returns>Array of collection items.</returns>
        public static T[] ToArray(IReadOnlyCollection<T> col)
        {
            var count = col.Count;
            if (count is 0)
                return [];

            var result = GC.AllocateUninitializedArray<T>(count);
            nuint index = 0;

            foreach (var item in col)
                result[index++] = item;

            return result;
        }
        
        /// <summary>
        /// Returns lazily converted read-only collection.
        /// </summary>
        /// <typeparam name="TOutput">Type of items in the target collection.</typeparam>
        /// <param name="converter">A collection item conversion function.</param>
        /// <returns>Lazily converted read-only collection.</returns>
        public ReadOnlyCollectionView<T, TOutput> Convert<TOutput>(Converter<T, TOutput> converter)
            => new(collection, converter);
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
        var hashCode = sequence.Aggregate(-910176598, static (hash, obj) => hash * hashSalt + (obj is null ? 0 : EqualityComparer<T>.Default.GetHashCode(obj)));
        return salted ? hashCode * hashSalt + RandomExtensions.BitwiseHashSalt : hashCode;
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
                list.ForEach(action);
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
    /// Obtains the first element of a sequence; or <see cref="Optional{T}.None"/>
    /// if the sequence is empty.
    /// </summary>
    /// <param name="collection">The collection to return the first element of.</param>
    /// <typeparam name="T">The type of the element of a collection.</typeparam>
    /// <returns>The first element; or <see cref="Optional{T}.None"/></returns>
    public static Optional<T> FirstOrNone<T>(this IEnumerable<T> collection)
    {
        return collection switch
        {
            null => throw new ArgumentNullException(nameof(collection)),
            List<T> list => Span.FirstOrNone<T>(CollectionsMarshal.AsSpan(list)),
            T[] array => Span.FirstOrNone<T>(array),
            string str => Unsafe.BitCast<Optional<char>, Optional<T>>(Span.FirstOrNone<char>(str)),
            LinkedList<T> list => list.First is { } first ? first.Value : Optional<T>.None,
            IList<T> list => list.Count > 0 ? list[0] : Optional<T>.None,
            IReadOnlyList<T> readOnlyList => readOnlyList.Count > 0 ? readOnlyList[0] : Optional<T>.None,
            _ => FirstOrNoneSlow(collection),
        };

        static Optional<T> FirstOrNoneSlow(IEnumerable<T> collection)
        {
            using var enumerator = collection.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : Optional<T>.None;
        }
    }

    /// <summary>
    /// Obtains the last element of a sequence; or <see cref="Optional{T}.None"/>
    /// if the sequence is empty.
    /// </summary>
    /// <param name="collection">The collection to return the first element of.</param>
    /// <typeparam name="T">The type of the element of a collection.</typeparam>
    /// <returns>The first element; or <see cref="Optional{T}.None"/></returns>
    public static Optional<T> LastOrNone<T>(this IEnumerable<T> collection)
    {
        return collection switch
        {
            null => throw new ArgumentNullException(nameof(collection)),
            List<T> list => Span.LastOrNone<T>(CollectionsMarshal.AsSpan(list)),
            T[] array => Span.LastOrNone<T>(array),
            string str => Unsafe.BitCast<Optional<char>, Optional<T>>(Span.LastOrNone<char>(str)),
            LinkedList<T> list => list.Last is { } last ? last.Value : Optional<T>.None,
            IList<T> list => list.Count > 0 ? list[^1] : Optional<T>.None,
            IReadOnlyList<T> readOnlyList => readOnlyList.Count > 0 ? readOnlyList[^1] : Optional<T>.None,
            _ => LastOrNoneSlow(collection),
        };

        static Optional<T> LastOrNoneSlow(IEnumerable<T> collection)
        {
            var result = Optional.None<T>();
            foreach (var item in collection)
            {
                result = item;
            }

            return result;
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
    /// Converts the synchronous collection of elements to asynchronous.
    /// </summary>
    /// <param name="enumerable">The collection of elements.</param>
    /// <param name="yieldIteration"><see langword="true"/> to execute every iteration asynchronously; otherwise, <see langword="false"/>.</param>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>The asynchronous wrapper over the synchronous collection of elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is <see langword="null"/>.</exception>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable, bool yieldIteration)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        return yieldIteration
            ? new AsyncEnumerable.YieldingEnumerable<T>(enumerable)
            : enumerable.ToAsyncEnumerable();
    }
}