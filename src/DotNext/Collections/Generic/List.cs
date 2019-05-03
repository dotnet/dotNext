using System;
using System.Collections.Generic;
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
                throw new MissingMemberException();
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
		public static Func<int, T> IndexerGetter<T>(this IReadOnlyList<T> list) => Indexer<T>.ReadOnly.Method.CreateDelegate<Func<int, T>>(list);

        /// <summary>
        /// Returns <see cref="IList{T}.get_Item"/> as delegate
        /// attached to the list instance. 
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
		public static Func<int, T> IndexerGetter<T>(this IList<T> list) => Indexer<T>.Getter.Method.CreateDelegate<Func<int, T>>(list);

        /// <summary>
        /// Returns <see cref="IList{T}.set_Item"/> as delegate
        /// attached to the list instance.
        /// </summary>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <param name="list">Mutable list instance.</param>
        /// <returns>A delegate representing indexer.</returns>
		public static Action<int, T> IndexerSetter<T>(this IList<T> list) => Indexer<T>.Setter.Method.CreateDelegate<Action<int, T>>(list);

        /// <summary>
        /// Converts list into array and perform mapping for each
        /// element.
        /// </summary>
        /// <typeparam name="I">Type of elements in the list.</typeparam>
        /// <typeparam name="O">Type of elements in the output array.</typeparam>
        /// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
        /// <param name="mapper">Element mapping function.</param>
        /// <returns>An array of list items.</returns>
        public static O[] ToArray<I, O>(this IList<I> input, Converter<I, O> mapper)
        {
            var output = OneDimensionalArray.New<O>(input.Count);
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper(input[i]);
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
        {
            var output = OneDimensionalArray.New<O>(input.Count);
            for (var i = 0; i < input.Count; i++)
                output[i] = mapper(i, input[i]);
            return output;
        }

        /// <summary>
        /// Returns read-only view of the list. 
        /// </summary>
        /// <param name="list">A list to be wrapped into read-only representation.</param>
        /// <typeparam name="T">Type of items in the list.</typeparam>
        /// <returns>Read-only view of the list.</returns>
        public static ReadOnlyListView<T> AsReadOnlyView<T>(this IList<T> list) => new ReadOnlyListView<T>(list);

        /// <summary>
        /// Returns lazily converted read-only list.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="converter">A list item conversion function.</param>
        /// <typeparam name="I">Type of items in the source list.</typeparam>
        /// <typeparam name="O">Type of items in the target list.</typeparam>
        /// <returns>Lazily converted read-only list.</returns>
        public static ReadOnlyListView<I, O> Convert<I, O>(this IReadOnlyList<I> list, Converter<I, O> converter) => new ReadOnlyListView<I, O>(list, converter);

        /// <summary>
        /// Constructs read-only list with single item in it.
        /// </summary>
        /// <param name="item">An item to be placed into list.</param>
        /// <typeparam name="T">Type of list items.</typeparam>
        /// <returns>Read-only list containing single item.</returns>
        public static IReadOnlyList<T> Singleton<T>(T item) => new SingletonList<T> { Item1 = item };
    }
}
