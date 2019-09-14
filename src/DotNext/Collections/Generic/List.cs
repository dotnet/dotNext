using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Provides various extensions for <see cref="IList{T}"/> interface.
    /// </summary>
    public static class List
    {
        private static class Indexer<C, T>
            where C : class, IEnumerable<T>
        {
            internal static readonly Func<C, int, T> Getter;
            internal static readonly Action<C, int, T> Setter;

            static Indexer()
            {
                foreach (var member in typeof(C).GetDefaultMembers())
                    if (member is PropertyInfo indexer)
                    {
                        Getter = indexer.GetMethod.CreateDelegate<Func<C, int, T>>();
                        Setter = indexer.SetMethod?.CreateDelegate<Action<C, int, T>>();
                        return;
                    }
                Debug.Fail(ExceptionMessages.UnreachableCodeDetected);
            }
        }

        /// <summary>
        /// Provides strongly-typed access to list indexer.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        public static class Indexer<T>
        {
            /// <summary>
            /// Represents read-only list item getter.
            /// </summary>
            public static Func<IReadOnlyList<T>, int, T> ReadOnly => Indexer<IReadOnlyList<T>, T>.Getter;

            /// <summary>
            /// Represents list item getter.
            /// </summary>
            public static Func<IList<T>, int, T> Getter => Indexer<IList<T>, T>.Getter;

            /// <summary>
            /// Represents list item setter.
            /// </summary>
            public static Action<IList<T>, int, T> Setter => Indexer<IList<T>, T>.Setter;
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyList{T}.get_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Read-only list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
		public static Func<int, T> IndexerGetter<T>(this IReadOnlyList<T> list) => Indexer<T>.ReadOnly.Bind(list);

        /// <summary>
        /// Returns <see cref="IList{T}.get_Item"/> as delegate
        /// attached to the list instance. 
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
		public static Func<int, T> IndexerGetter<T>(this IList<T> list) => Indexer<T>.Getter.Bind(list);

        /// <summary>
        /// Returns <see cref="IList{T}.set_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
		public static Action<int, T> IndexerSetter<T>(this IList<T> list) => Indexer<T>.Setter.Bind(list);

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="I">Type of elements in the list.</typeparam>
        /// <typeparam name="O">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static O[] ToArray<I, O>(this IList<I> input, in ValueFunc<I, O> mapper)
        {
            var output = OneDimensionalArray.New<O>(input.Count);
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper.Invoke(input[i]);
            return output;
        }

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="I">Type of elements in the list.</typeparam>
        /// <typeparam name="O">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static O[] ToArray<I, O>(this IList<I> input, Converter<I, O> mapper) => ToArray(input, mapper.AsValueFunc(true));

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="I">Type of elements in the list.</typeparam>
        /// <typeparam name="O">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Index-aware element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static O[] ToArray<I, O>(this IList<I> input, in ValueFunc<int, I, O> mapper)
        {
            var output = OneDimensionalArray.New<O>(input.Count);
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper.Invoke(i, input[i]);
            return output;
        }

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="I">Type of elements in the list.</typeparam>
        /// <typeparam name="O">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Index-aware element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static O[] ToArray<I, O>(this IList<I> input, Func<int, I, O> mapper)
            => ToArray(input, new ValueFunc<int, I, O>(mapper, true));

        /// <summary>
        /// Returns lazily converted read-only list.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="converter">A list item conversion function.</param>
        /// <typeparam name="I">Type of items in the source list.</typeparam>
        /// <typeparam name="O">Type of items in the target list.</typeparam>
        /// <returns>Lazily converted read-only list.</returns>
        public static ReadOnlyListView<I, O> Convert<I, O>(this IReadOnlyList<I> list, in ValueFunc<I, O> converter) => new ReadOnlyListView<I, O>(list, converter);

        /// <summary>
        /// Returns lazily converted read-only list.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="converter">A list item conversion function.</param>
        /// <typeparam name="I">Type of items in the source list.</typeparam>
        /// <typeparam name="O">Type of items in the target list.</typeparam>
        /// <returns>Lazily converted read-only list.</returns>
        public static ReadOnlyListView<I, O> Convert<I, O>(this IReadOnlyList<I> list, Converter<I, O> converter)
            => Convert(list, converter.AsValueFunc(true));

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
    }
}
