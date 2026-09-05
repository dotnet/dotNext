using System.Collections;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Collections.Generic;

using static Reflection.CollectionType;

/// <summary>
/// Provides various extensions for <see cref="IList{T}"/> interface.
/// </summary>
public static class List
{
    /// <summary>
    /// Extends <see cref="IReadOnlyList{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    /// <param name="list">Read-only list instance.</param>
    extension<T>(IReadOnlyList<T> list)
    {
        /// <summary>
        /// Returns <see cref="IReadOnlyList{T}.get_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <value>A delegate representing indexer.</value>
        public Func<int, T> IndexerGetter
        {
            get
            {
                Push(list);
                Dup();
                Ldvirtftn(PropertyGet(Type<IReadOnlyList<T>>(), ItemIndexerName));
                Newobj(Constructor(Type<Func<int, T>>(), Type<object>(), Type<IntPtr>()));
                return Return<Func<int, T>>();
            }
        }
        
        /// <summary>
        /// Constructs read-only list with a single item in it.
        /// </summary>
        /// <param name="item">An item to be placed into list.</param>
        /// <returns>Read-only list containing single item.</returns>
        public static IReadOnlyList<T> Singleton(T item) => new Specialized.SingletonList<T> { Item = item };

        /// <summary>
        /// Generates a list that contains one repeated value.
        /// </summary>
        /// <param name="item">The item to be returned from the list.</param>
        /// <param name="count">The number of elements in the list.</param>
        /// <returns>A list that contains a repeated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        public static IReadOnlyList<T> Repeat(T item, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            return count switch
            {
                0 => [],
                1 => Singleton(item),
                _ => new RepeatList<T>(item, count),
            };
        }
    }

    /// <summary>
    /// Extends <see cref="IList{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    /// <param name="list">Mutable list instance.</param>
    extension<T>(IList<T> list)
    {
        /// <summary>
        /// Returns <see cref="IList{T}.get_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <value>A delegate representing indexer.</value>
        public Func<int, T> IndexerGetter
        {
            get
            {
                Push(list);
                Dup();
                Ldvirtftn(PropertyGet(Type<IList<T>>(), ItemIndexerName));
                Newobj(Constructor(Type<Func<int, T>>(), Type<object>(), Type<IntPtr>()));
                return Return<Func<int, T>>();
            }
        }

        /// <summary>
        /// Returns <see cref="IList{T}.set_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <returns>A delegate representing indexer.</returns>
        public Action<int, T> IndexerSetter
        {
            get
            {
                Push(list);
                Dup();
                Ldvirtftn(PropertySet(Type<IList<T>>(), ItemIndexerName));
                Newobj(Constructor(Type<Action<int, T>>(), Type<object>(), Type<IntPtr>()));
                return Return<Action<int, T>>();
            }
        }

        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// </remarks>
        /// <typeparam name="TComparer">The type of the comparer providing comparison logic.</typeparam>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        public int InsertOrdered<TComparer>(T item, TComparer comparer)
            where TComparer : IComparer<T>
        {
            var index = GetInsertionPosition(new ReadOnlyList<T>(list), item, comparer);
            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// </remarks>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        public int InsertOrdered(T item, Comparison<T?> comparer)
            => InsertOrdered<T, DelegatingComparer<T>>(list, item, comparer);

        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// </remarks>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        [CLSCompliant(false)]
        public unsafe int InsertOrdered(T item, delegate*<T?, T?, int> comparer)
            => InsertOrdered<T, ComparerWrapper<T>>(list, item, comparer);
        
        /// <summary>
        /// Inserts an item to the list at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The object to insert into the list.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the receiver.</exception>
        /// <exception cref="NotSupportedException">The receiver is read-only.</exception>
        public void Insert(Index index, T item)
            => list.Insert(index.GetOffset(list.Count), item);

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the receiver.</exception>
        /// <exception cref="NotSupportedException">The receiver is read-only.</exception>
        public void RemoveAt(Index index)
            => list.RemoveAt(index.GetOffset(list.Count));

        /// <summary>
        /// Returns slice of the list.
        /// </summary>
        /// <param name="range">The range of elements in the list.</param>
        /// <returns>The section of the list.</returns>
        public ListSegment<T> Slice(Range range)
            => new(list, range);
    }

    /// <summary>
    /// Converts list into array and perform mapping for each
    /// element.
    /// </summary>
    /// <typeparam name="TInput">Type of elements in the list.</typeparam>
    /// <typeparam name="TOutput">Type of elements in the output array.</typeparam>
    /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
    /// <param name="mapper">Element mapping function.</param>
    /// <returns>An array of list items.</returns>
    public static TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, Converter<TInput, TOutput> mapper)
        => ToArray<TInput, ReadOnlyList<TInput>, TOutput, DelegatingConverter<TInput, TOutput>>(new(input), mapper);

    /// <summary>
    /// Converts list into array and perform mapping for each
    /// element.
    /// </summary>
    /// <typeparam name="TInput">Type of elements in the list.</typeparam>
    /// <typeparam name="TOutput">Type of elements in the output array.</typeparam>
    /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
    /// <param name="mapper">Element mapping function.</param>
    /// <returns>An array of list items.</returns>
    [CLSCompliant(false)]
    public static unsafe TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, delegate*<TInput, TOutput> mapper)
        => ToArray<TInput, ReadOnlyList<TInput>, TOutput, Supplier<TInput, TOutput>>(new(input), mapper);

    /// <summary>
    /// Converts list into array and perform mapping for each
    /// element.
    /// </summary>
    /// <typeparam name="TInput">Type of elements in the list.</typeparam>
    /// <typeparam name="TOutput">Type of elements in the output array.</typeparam>
    /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
    /// <param name="mapper">Index-aware element mapping function.</param>
    /// <returns>An array of list items.</returns>
    public static TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, Func<int, TInput, TOutput> mapper)
        => ToArrayWithIndex<TInput, ReadOnlyList<TInput>, TOutput, DelegatingSupplier<int, TInput, TOutput>>(new(input), mapper);

    /// <summary>
    /// Converts list into array and perform mapping for each
    /// element.
    /// </summary>
    /// <typeparam name="TInput">Type of elements in the list.</typeparam>
    /// <typeparam name="TOutput">Type of elements in the output array.</typeparam>
    /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
    /// <param name="mapper">Index-aware element mapping function.</param>
    /// <returns>An array of list items.</returns>
    [CLSCompliant(false)]
    public static unsafe TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, delegate*<int, TInput, TOutput> mapper)
        => ToArrayWithIndex<TInput, ReadOnlyList<TInput>, TOutput, Supplier<int, TInput, TOutput>>(new(input), mapper);

    /// <summary>
    /// Returns lazily converted read-only list.
    /// </summary>
    /// <param name="list">Read-only list to convert.</param>
    /// <param name="converter">A list item conversion function.</param>
    /// <typeparam name="TInput">Type of items in the source list.</typeparam>
    /// <typeparam name="TOutput">Type of items in the target list.</typeparam>
    /// <returns>Lazily converted read-only list.</returns>
    public static ReadOnlyListView<TInput, TOutput> Convert<TInput, TOutput>(this IReadOnlyList<TInput> list, Func<TInput, TOutput> converter)
        => new(list, converter);

    /// <summary>
    /// Extends <see cref="List{T}"/> type.
    /// </summary>
    /// <param name="list">The list to insert into.</param>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    extension<T>(List<T> list)
    {
        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// This version method is specially optimized for <see cref="List{T}"/> data type
        /// while <see cref="InsertOrdered{T, TComparer}(IList{T}, T, TComparer)"/>
        /// is for generic list of unknown type.
        /// </remarks>
        /// <typeparam name="TComparer">The type of the comparer providing comparison logic.</typeparam>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        public int InsertOrdered<TComparer>(T item, TComparer comparer)
            where TComparer : IComparer<T>
        {
            var index = GetInsertionPosition(list, item, comparer);
            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Removes a range of elements from list.
        /// </summary>
        /// <param name="range">The range of elements to be removed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is invalid.</exception>
        public void RemoveRange(Range range)
        {
            var (start, length) = range.GetOffsetAndLength(list.Count);
            list.RemoveRange(start, length);
        }
    }
    
    private static int GetInsertionPosition<T, TList, TComparer>(TList list, T item, TComparer comparer)
        where TList : IReadOnlyList<T>
        where TComparer : IComparer<T>
    {
        var low = 0;
        for (var high = list.Count; low < high;)
        {
            var mid = (low + high) / 2;
            var cmp = comparer.Compare(list[mid], item);
            if (cmp > 0)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    private static TOutput[] ToArray<TInput, TList, TOutput, TConverter>(TList list, TConverter mapper)
        where TList : IReadOnlyList<TInput>
        where TConverter : struct, ISupplier<TInput, TOutput>
    {
        var count = list.Count;
        if (count is 0)
            return [];

        var output = GC.AllocateUninitializedArray<TOutput>(count);
        for (var i = 0; i < count; i++)
            output[i] = mapper.Invoke(list[i]);

        return output;
    }

    private static TOutput[] ToArrayWithIndex<TInput, TList, TOutput, TConverter>(TList list, TConverter mapper)
        where TList : IReadOnlyList<TInput>
        where TConverter : struct, ISupplier<int, TInput, TOutput>
    {
        var count = list.Count;
        var output = GC.AllocateUninitializedArray<TOutput>(count);
        for (var i = 0; i < count; i++)
            output[i] = mapper.Invoke(i, list[i]);

        return output;
    }
}

[StructLayout(LayoutKind.Auto)]
file readonly struct ReadOnlyList<T>(IList<T> list) : IReadOnlyList<T>
{
    public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => list.Count;

    public T this[int index] => list[index];
}

file sealed class RepeatList<T>(T item, int count) : IReadOnlyList<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < count; i++)
            yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => count;

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)count, nameof(index));

            return item;
        }
    }
}