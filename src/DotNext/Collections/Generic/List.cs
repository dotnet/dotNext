using System;
using System.Collections.Generic;
using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Collections.Generic
{
    using static Reflection.CollectionType;

    /// <summary>
    /// Provides various extensions for <see cref="IList{T}"/> interface.
    /// </summary>
    public static class List
    {
        /// <summary>
        /// Provides strongly-typed access to list indexer.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        public static class Indexer<T>
        {
            /// <summary>
            /// Represents read-only list item getter.
            /// </summary>
            public static Func<IReadOnlyList<T>, int, T> ReadOnly { get; }

            /// <summary>
            /// Represents list item getter.
            /// </summary>
            public static Func<IList<T>, int, T> Getter { get; }

            /// <summary>
            /// Represents list item setter.
            /// </summary>
            public static Action<IList<T>, int, T> Setter { get; }

            static Indexer()
            {
                Ldtoken(PropertyGet(Type<IReadOnlyList<T>>(), ItemIndexerName));
                Pop(out RuntimeMethodHandle method);
                Ldtoken(Type<IReadOnlyList<T>>());
                Pop(out RuntimeTypeHandle type);
                ReadOnly = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IReadOnlyList<T>, int, T>>();

                Ldtoken(PropertyGet(Type<IList<T>>(), ItemIndexerName));
                Pop(out method);
                Ldtoken(Type<IList<T>>());
                Pop(out type);
                Getter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IList<T>, int, T>>();

                Ldtoken(PropertySet(Type<IList<T>>(), ItemIndexerName));
                Pop(out method);
                Setter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Action<IList<T>, int, T>>();
            }
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyList{T}.get_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Read-only list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
        public static Func<int, T> IndexerGetter<T>(this IReadOnlyList<T> list)
        {
            Push(list);
            Dup();
            Ldvirtftn(PropertyGet(Type<IReadOnlyList<T>>(), ItemIndexerName));
            Newobj(Constructor(Type<Func<int, T>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<int, T>>();
        }

        /// <summary>
        /// Returns <see cref="IList{T}.get_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
        public static Func<int, T> IndexerGetter<T>(this IList<T> list)
        {
            Push(list);
            Dup();
            Ldvirtftn(PropertyGet(Type<IList<T>>(), ItemIndexerName));
            Newobj(Constructor(Type<Func<int, T>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<int, T>>();
        }

        /// <summary>
        /// Returns <see cref="IList{T}.set_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
        public static Action<int, T> IndexerSetter<T>(this IList<T> list)
        {
            Push(list);
            Dup();
            Ldvirtftn(PropertySet(Type<IList<T>>(), ItemIndexerName));
            Newobj(Constructor(Type<Action<int, T>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<int, T>>();
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
        public static TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, in ValueFunc<TInput, TOutput> mapper)
        {
#if NETSTANDARD2_1
            var output = OneDimensionalArray.New<TOutput>(input.Count);
#else
            var output = GC.AllocateUninitializedArray<TOutput>(input.Count);
#endif
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper.Invoke(input[i]);
            return output;
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
        public static TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, Converter<TInput, TOutput> mapper) => ToArray(input, mapper.AsValueFunc());

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="TInput">Type of elements in the list.</typeparam>
        /// <typeparam name="TOutput">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Index-aware element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static TOutput[] ToArray<TInput, TOutput>(this IList<TInput> input, in ValueFunc<int, TInput, TOutput> mapper)
        {
#if NETSTANDARD2_1
            var output = OneDimensionalArray.New<TOutput>(input.Count);
#else
            var output = GC.AllocateUninitializedArray<TOutput>(input.Count);
#endif
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper.Invoke(i, input[i]);
            return output;
        }

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
            => ToArray(input, new ValueFunc<int, TInput, TOutput>(mapper));

        /// <summary>
        /// Returns lazily converted read-only list.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="converter">A list item conversion function.</param>
        /// <typeparam name="TInput">Type of items in the source list.</typeparam>
        /// <typeparam name="TOutput">Type of items in the target list.</typeparam>
        /// <returns>Lazily converted read-only list.</returns>
        public static ReadOnlyListView<TInput, TOutput> Convert<TInput, TOutput>(this IReadOnlyList<TInput> list, in ValueFunc<TInput, TOutput> converter) => new ReadOnlyListView<TInput, TOutput>(list, converter);

        /// <summary>
        /// Returns lazily converted read-only list.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="converter">A list item conversion function.</param>
        /// <typeparam name="TInput">Type of items in the source list.</typeparam>
        /// <typeparam name="TOutput">Type of items in the target list.</typeparam>
        /// <returns>Lazily converted read-only list.</returns>
        public static ReadOnlyListView<TInput, TOutput> Convert<TInput, TOutput>(this IReadOnlyList<TInput> list, Converter<TInput, TOutput> converter)
            => Convert(list, converter.AsValueFunc());

        /// <summary>
        /// Constructs read-only list with single item in it.
        /// </summary>
        /// <param name="item">An item to be placed into list.</param>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <returns>Read-only list containing single item.</returns>
        public static IReadOnlyList<T> Singleton<T>(T item) => new SingletonList<T>(item);

        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// </remarks>
        /// <typeparam name="T">The type of the items in the list.</typeparam>
        /// <param name="list">The list to insert into.</param>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        public static int InsertOrdered<T>(this IList<T> list, T item, in ValueFunc<T, T, int> comparer)
        {
            int low = 0, high = list.Count;
            while (low < high)
            {
                var mid = (low + high) / 2;
                var cmp = comparer.Invoke(list[mid], item);
                if (cmp > 0)
                    high = mid;
                else
                    low = mid + 1;
            }

            list.Insert(low, item);
            return low;
        }

        /// <summary>
        /// Inserts the item into sorted list.
        /// </summary>
        /// <remarks>
        /// Time complexity of this operation is O(log N), where N is a size of the list.
        /// </remarks>
        /// <typeparam name="T">The type of the items in the list.</typeparam>
        /// <param name="list">The list to insert into.</param>
        /// <param name="item">The item to be added into the list.</param>
        /// <param name="comparer">The comparer function.</param>
        /// <returns>The actual index of the inserted item.</returns>
        public static int InsertOrdered<T>(this IList<T> list, T item, Comparison<T> comparer) => InsertOrdered(list, item, comparer.AsValueFunc());

        /// <summary>
        /// Removes a range of elements from list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to modify.</param>
        /// <param name="range">The range of elements to be removed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is invalid.</exception>
        public static void RemoveRange<T>(this List<T> list, Range range)
        {
            var (start, length) = range.GetOffsetAndLength(list.Count);
            list.RemoveRange(start, length);
        }

        /// <summary>
        /// Inserts an item to the list at the specified index.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to modify.</param>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The object to insert into the list.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in <paramref name="list"/>.</exception>
        /// <exception cref="NotSupportedException"><paramref name="list"/> is read-only.</exception>
        public static void Insert<T>(this IList<T> list, Index index, T item)
            => list.Insert(index.GetOffset(list.Count), item);

        /// <summary>
        /// Removes the item at the specifie index.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to modify.</param>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in <paramref name="list"/>.</exception>
        /// <exception cref="NotSupportedException"><paramref name="list"/> is read-only.</exception>
        public static void RemoveAt<T>(this IList<T> list, Index index)
            => list.RemoveAt(index.GetOffset(list.Count));

        /// <summary>
        /// Returns slice of the list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list of elements.</param>
        /// <param name="range">The range of elements in the list.</param>
        /// <returns>The section of the list.</returns>
        public static ListSegment<T> Slice<T>(this IList<T> list, Range range)
            => new ListSegment<T>(list, range);
    }
}
