using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

using static Runtime.Intrinsics;

/// <summary>
/// Various methods to work with classes implementing <see cref="IEnumerable{T}"/> interface.
/// </summary>
public static partial class Sequence
{
    /// <summary>
    /// Computes hash code for the sequence of objects.
    /// </summary>
    /// <param name="sequence">The sequence of elements.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>The hash code computed from each element in the sequence.</returns>
    public static int SequenceHashCode(this IEnumerable<object?> sequence, bool salted = true)
    {
        const int hashSalt = -1521134295;
        var hashCode = sequence.Aggregate(-910176598, static (hash, obj) => (hash * hashSalt) + obj?.GetHashCode() ?? 0);
        return salted ? (hashCode * hashSalt) + RandomExtensions.BitwiseHashSalt : hashCode;
    }

    internal static bool SequenceEqual(IEnumerable<object>? first, IEnumerable<object>? second)
        => first is null || second is null ? ReferenceEquals(first, second) : Enumerable.SequenceEqual(first, second);

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
    /// Obtains first value type in the sequence; or <see langword="null"/>
    /// if sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
    public static T? FirstOrNull<T>(this IEnumerable<T> seq)
        where T : struct
        => FirstOrNone(seq).OrNull();

    /// <summary>
    /// Obtains the last value type in the sequence; or <see langword="null"/>
    /// if sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <returns>The last element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
    public static T? LastOrNull<T>(this IEnumerable<T> seq)
        where T : struct
        => LastOrNone(seq).OrNull();

    /// <summary>
    /// Obtains first value in the sequence; or <see cref="Optional{T}.None"/>
    /// if sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
    [Obsolete("Use FirstOrNone() extension method instead")]
    public static Optional<T> FirstOrEmpty<T>(this IEnumerable<T> seq)
        => FirstOrNone(seq);

    /// <summary>
    /// Obtains first value in the sequence; or <see cref="Optional{T}.None"/>
    /// if sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
    public static Optional<T> FirstOrNone<T>(this IEnumerable<T> seq)
    {
        return seq switch
        {
            List<T> list => Span.FirstOrNone<T>(CollectionsMarshal.AsSpan(list)),
            T[] array => Span.FirstOrNone<T>(array),
            string str => ReinterpretCast<Optional<char>, Optional<T>>(str.AsSpan().FirstOrNone()), // Workaround for https://github.com/dotnet/runtime/issues/57484
            IList<T> list => list.Count > 0 ? list[0] : Optional<T>.None,
            IReadOnlyList<T> list => list.Count > 0 ? list[0] : Optional<T>.None,
            _ => FirstOrNoneSlow(seq),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Optional<T> FirstOrNoneSlow(IEnumerable<T> seq)
        {
            using var enumerator = seq.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : Optional<T>.None;
        }
    }

    /// <summary>
    /// Obtains first value in the sequence; or <see cref="Optional{T}.None"/>
    /// if sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
    public static Optional<T> LastOrNone<T>(this IEnumerable<T> seq)
    {
        return seq switch
        {
            List<T> list => Span.LastOrNone<T>(CollectionsMarshal.AsSpan(list)),
            T[] array => Span.LastOrNone<T>(array),
            string str => ReinterpretCast<Optional<char>, Optional<T>>(str.AsSpan().LastOrNone()), // Workaround for https://github.com/dotnet/runtime/issues/57484
            IList<T> list => list.Count > 0 ? list[list.Count - 1] : Optional<T>.None,
            IReadOnlyList<T> list => list.Count > 0 ? list[list.Count - 1] : Optional<T>.None,
            _ => LastOrNoneSlow(seq),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Optional<T> LastOrNoneSlow(IEnumerable<T> seq)
        {
            var result = Optional<T>.None;
            foreach (var item in seq)
                result = item;

            return result;
        }
    }

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="seq">A collection to return an element from.</param>
    /// <param name="filter">A function to test each element for a condition.</param>
    /// <returns>The first element in the sequence that matches to the specified filter; or <see cref="Optional{T}.None"/>.</returns>
    [Obsolete("Use FirstOrNone() extension method instead")]
    public static Optional<T> FirstOrEmpty<T>(this IEnumerable<T> seq, Predicate<T> filter)
        => FirstOrNone(seq, filter);

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="seq">A collection to return an element from.</param>
    /// <param name="filter">A function to test each element for a condition.</param>
    /// <returns>The first element in the sequence that matches to the specified filter; or <see cref="Optional{T}.None"/>.</returns>
    public static Optional<T> FirstOrNone<T>(this IEnumerable<T> seq, Predicate<T> filter)
    {
        return seq switch
        {
            List<T> list => Span.FirstOrNone(CollectionsMarshal.AsSpan(list), filter),
            T[] array => Span.FirstOrNone(array, filter),
            string str => ReinterpretCast<Optional<char>, Optional<T>>(str.AsSpan().FirstOrNone(Unsafe.As<Predicate<char>>(filter))),
            LinkedList<T> list => FindInLinkedList(list, filter),
            _ => FirstOrNoneSlow(seq, filter)
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Optional<T> FindInLinkedList(LinkedList<T> list, Predicate<T> filter)
        {
            for (var node = list.First; node is not null; node = node.Next)
            {
                var value = node.Value;
                if (filter(value))
                    return value;
            }

            return Optional<T>.None;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Optional<T> FirstOrNoneSlow(IEnumerable<T> seq, Predicate<T> filter)
        {
            foreach (var item in seq)
            {
                if (filter.Invoke(item))
                    return item;
            }

            return Optional<T>.None;
        }
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    public static bool Skip<T>(this IEnumerator<T> enumerator, int count)
    {
        while (count > 0)
        {
            if (!enumerator.MoveNext())
                return false;

            count--;
        }

        return true;
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="TEnumerator">The type of the sequence.</typeparam>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    public static bool Skip<TEnumerator, T>(this ref TEnumerator enumerator, int count)
        where TEnumerator : struct, IEnumerator<T>
    {
        while (count > 0)
        {
            if (!enumerator.MoveNext())
                return false;

            count--;
        }

        return true;
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
    /// Constructs a sequence from the single element.
    /// </summary>
    /// <typeparam name="T">Type of element.</typeparam>
    /// <param name="item">An item to be placed into sequence.</param>
    /// <returns>Sequence of single element.</returns>
    public static IEnumerable<T> Singleton<T>(T item)
        => List.Singleton(item);

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
    /// Limits the number of the elements in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of items in the sequence.</typeparam>
    /// <param name="enumerator">The sequence of the elements.</param>
    /// <param name="count">The maximum number of the elements in the returned sequence.</param>
    /// <param name="leaveOpen"><see langword="false"/> to dispose <paramref name="enumerator"/>; otherwise, <see langword="true"/>.</param>
    /// <returns>The enumerator which is limited by count.</returns>
    public static LimitedEnumerator<T> Limit<T>(this IEnumerator<T> enumerator, int count, bool leaveOpen = false)
        => new(enumerator, count, leaveOpen);

    /// <summary>
    /// Gets enumerator over all elements in the memory.
    /// </summary>
    /// <param name="memory">The memory block to be converted.</param>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <returns>The enumerator over all elements in the memory.</returns>
    /// <seealso cref="System.Runtime.InteropServices.MemoryMarshal.ToEnumerable{T}(ReadOnlyMemory{T})"/>
    public static IEnumerator<T> ToEnumerator<T>(ReadOnlyMemory<T> memory)
    {
        return memory.IsEmpty
            ? GetEmptyEnumerator<T>()
            : MemoryMarshal.TryGetArray(memory, out var segment)
            ? segment.GetEnumerator()
            : ToEnumeratorSlow(memory);

        static IEnumerator<T> ToEnumeratorSlow(ReadOnlyMemory<T> memory)
        {
            for (nint i = 0; i < memory.Length; i++)
                yield return Unsafe.Add(ref MemoryMarshal.GetReference(memory.Span), i);
        }
    }

    /// <summary>
    /// Gets enumerator over all elements in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="sequence">A sequence of elements.</param>
    /// <returns>The enumerator over all elements in the sequence.</returns>
    public static IEnumerator<T> ToEnumerator<T>(in ReadOnlySequence<T> sequence)
    {
        return sequence.IsEmpty
            ? GetEmptyEnumerator<T>()
            : sequence.IsSingleSegment
            ? ToEnumerator(sequence.First)
            : ToEnumeratorSlow(sequence.GetEnumerator());

        static IEnumerator<T> ToEnumeratorSlow(ReadOnlySequence<T>.Enumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                var segment = enumerator.Current;

                for (nint i = 0; i < segment.Length; i++)
                    yield return Unsafe.Add(ref MemoryMarshal.GetReference(segment.Span), i);
            }
        }
    }
}
