using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;
using ITuple = System.Runtime.CompilerServices.ITuple;

namespace DotNext
{
    using static Runtime.Intrinsics;

    /// <summary>
    /// Represents tuple as enumerable collection.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the tuple.</typeparam>
    /// <typeparam name="TTuple">The tuple type.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [Obsolete("Use Span.AsSpan(ref ValueTuple) extension methods instead")]
    public readonly struct EnumerableTuple<TItem, TTuple> : IReadOnlyList<TItem>
        where TTuple : ITuple
    {
        /// <summary>
        /// Represents enumerator over items in the tuple.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        [Obsolete("Use Span.AsSpan(ref ValueTuple) extension methods instead")]
        public struct Enumerator : IEnumerator<TItem>
        {
            private const int InitialPosition = -1;
            private readonly ValueRefFunc<TTuple, int, TItem> accessor;
            private TTuple tuple;
            private int currentIndex;

            internal Enumerator(TTuple tuple, in ValueRefFunc<TTuple, int, TItem> accessor)
            {
                this.tuple = tuple;
                currentIndex = InitialPosition;
                this.accessor = accessor;
            }

            /// <summary>
            /// Gets currently iterating item in the tuple.
            /// </summary>
            public TItem Current => accessor.Invoke(ref tuple, currentIndex);

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <summary>
            /// Advances position of this enumerator.
            /// </summary>
            /// <returns><see langword="true"/> if next item exists in the tuple; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext() => ++currentIndex < tuple.Length;

            /// <summary>
            /// Sets the enumerator to its initial position, which is before
            /// the first item in the tuple.
            /// </summary>
            public void Reset() => currentIndex = InitialPosition;

            /// <inheritdoc/>
            void IDisposable.Dispose() => this = default;
        }

        private readonly TTuple tuple;
        private readonly ValueRefFunc<TTuple, int, TItem> accessor;

        internal EnumerableTuple(TTuple tuple, in ValueRefFunc<TTuple, int, TItem> accessor)
        {
            this.tuple = tuple;
            this.accessor = accessor;
        }

        /// <summary>
        /// Gets tuple item by its index.
        /// </summary>
        /// <param name="index">The index of the item.</param>
        /// <returns>Item value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is invalid.</exception>
        public TItem this[int index] => accessor.IsEmpty ? throw new ArgumentOutOfRangeException(nameof(index)) : accessor.Invoke(ref AsRef(tuple), index);

        /// <summary>
        /// Gets number of items in the tuple.
        /// </summary>
        public int Count => tuple.Length;

        /// <summary>
        /// Copies tuple items into specified memory span.
        /// </summary>
        /// <param name="output">The memory span to be written.</param>
        /// <returns>The actual of modified elements in memory span.</returns>
        public int CopyTo(Span<TItem> output)
        {
            int count;
            for (count = 0; count < Math.Min(output.Length, tuple.Length); count++)
                output[count] = this[count];
            return count;
        }

        /// <summary>
        /// Gets enumerator over items in the tuple.
        /// </summary>
        /// <returns>The enumerator over items.</returns>
        public Enumerator GetEnumerator() => new Enumerator(tuple, accessor);

        /// <inheritdoc/>
        IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Provides static methods allow to convert tuples into enumerable collections.
    /// </summary>
    /// <seealso cref="Tuple"/>
    /// <see cref="ValueTuple"/>
    [Obsolete("Use Span.AsSpan(ref ValueTuple) extension methods instead")]
    public static class EnumerableTuple
    {
        private static TItem GetItem<TItem>(ref Tuple<TItem> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            2 => tuple.Item3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem, TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            2 => tuple.Item3,
            3 => tuple.Item4,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem, TItem, TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            2 => tuple.Item3,
            3 => tuple.Item4,
            4 => tuple.Item5,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem, TItem, TItem, TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            2 => tuple.Item3,
            3 => tuple.Item4,
            4 => tuple.Item5,
            5 => tuple.Item6,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static TItem GetItem<TItem>(ref Tuple<TItem, TItem, TItem, TItem, TItem, TItem, TItem> tuple, int index) => index switch
        {
            0 => tuple.Item1,
            1 => tuple.Item2,
            2 => tuple.Item3,
            3 => tuple.Item4,
            4 => tuple.Item5,
            5 => tuple.Item6,
            6 => tuple.Item7,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, ValueTuple<T>> AsEnumerable<T>(ValueTuple<T> tuple)
            => new EnumerableTuple<T, ValueTuple<T>>(tuple, new ValueRefFunc<ValueTuple<T>, int, T>(GetTupleItem<ValueTuple<T>, T>));

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T>> AsEnumerable<T>(Tuple<T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T>>(tuple, new ValueRefFunc<Tuple<T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T)> AsEnumerable<T>((T, T) tuple)
            => new EnumerableTuple<T, (T, T)>(tuple, new ValueRefFunc<(T, T), int, T>(GetTupleItem<(T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T>> AsEnumerable<T>(Tuple<T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T>>(tuple, new ValueRefFunc<Tuple<T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T)> AsEnumerable<T>((T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T)>(tuple, new ValueRefFunc<(T, T, T), int, T>(GetTupleItem<(T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T>> AsEnumerable<T>(Tuple<T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T>>(tuple, new ValueRefFunc<Tuple<T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T)> AsEnumerable<T>((T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T)>(tuple, new ValueRefFunc<(T, T, T, T), int, T>(GetTupleItem<(T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T>> AsEnumerable<T>(Tuple<T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T>>(tuple, new ValueRefFunc<Tuple<T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T)> AsEnumerable<T>((T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T)>(tuple, new ValueRefFunc<(T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T>> AsEnumerable<T>(Tuple<T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T>>(tuple, new ValueRefFunc<Tuple<T, T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T)> AsEnumerable<T>((T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T)>(tuple, new ValueRefFunc<(T, T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T>> AsEnumerable<T>(Tuple<T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T>>(tuple, new ValueRefFunc<Tuple<T, T, T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T, T)> AsEnumerable<T>((T, T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T, T)>(tuple, new ValueRefFunc<(T, T, T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>> AsEnumerable<T>(Tuple<T, T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>>(tuple, new ValueRefFunc<Tuple<T, T, T, T, T, T, T>, int, T>(GetItem));
    }
}