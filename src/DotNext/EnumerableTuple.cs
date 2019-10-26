using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext
{
    /// <summary>
    /// Represents tuple as enumerable collection.
    /// </summary>
    /// <typeparam name="I">The type of items in the tuple.</typeparam>
    /// <typeparam name="T">The tuple type.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct EnumerableTuple<I, T> : IReadOnlyList<I>, ITuple
        where T : IStructuralEquatable, IStructuralComparable, ITuple
    {
        //TODO: EnumerableTuple should implements ITuple, possible from .NET Standard 2.1

        internal delegate I ItemAccessor(in T tuple, int index);

        /// <summary>
        /// Represents enumerator over items in the tuple.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<I>
        {
            private readonly T tuple;
            private readonly ItemAccessor accessor;
            private int currentIndex;

            internal Enumerator(T tuple, ItemAccessor accessor)
            {
                this.tuple = tuple;
                currentIndex = -1;
                this.accessor = accessor;
            }

            /// <summary>
            /// Gets currently iterating item in the tuple.
            /// </summary>
            public readonly I Current => accessor(in tuple, currentIndex);

            readonly object IEnumerator.Current => Current;

            /// <summary>
            /// Advances position of this enumerator.
            /// </summary>
            /// <returns><see langword="true"/> if next item exists in the tuple; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext()
            {
                currentIndex += 1;
                return currentIndex < tuple.Length;
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before 
            /// the first item in the tuple.
            /// </summary>
            public void Reset() => currentIndex = -1;

            void IDisposable.Dispose() => this = default;
        }

        private readonly T tuple;
        private readonly ItemAccessor accessor;

        internal EnumerableTuple(T tuple, ItemAccessor accessor)
        {
            this.tuple = tuple;
            this.accessor = accessor;
        }

        /// <summary>
        /// Gets tuple item by its index.
        /// </summary>
        /// <param name="index">The index of the item.</param>
        /// <returns>Item value.</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is invalid.</exception>
        public I this[int index] => index >= 0 && index < Count ? accessor(tuple, index) : throw new IndexOutOfRangeException();

        /// <summary>
        /// Gets number of items in the tuple.
        /// </summary>
        public int Count => tuple.Length;

        int ITuple.Length => Count;

        object ITuple.this[int index] => tuple[index];

        /// <summary>
        /// Gets enumerator over items in the tuple.
        /// </summary>
        /// <returns>The enumerator over items.</returns>
        public Enumerator GetEnumerator() => new Enumerator(tuple, accessor);

        IEnumerator<I> IEnumerable<I>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Provides static methods allow to convert tuples into enumerable collections.
    /// </summary>
    /// <seealso cref="Tuple"/>
    /// <see cref="ValueTuple"/>
    public static class EnumerableTuple
    {
        private static E GetItem<E>(in ValueTuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static E GetItem<E>(in Tuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static E GetItem<E>(in ValueTuple<E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in Tuple<E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in ValueTuple<E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in Tuple<E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in ValueTuple<E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in Tuple<E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in ValueTuple<E, E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                4 => tuple.Item5,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in Tuple<E, E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                4 => tuple.Item5,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in ValueTuple<E, E, E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                4 => tuple.Item5,
                5 => tuple.Item6,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in Tuple<E, E, E, E, E, E> tuple, int index)
            => index switch
            {
                0 => tuple.Item1,
                1 => tuple.Item2,
                2 => tuple.Item3,
                3 => tuple.Item4,
                4 => tuple.Item5,
                5 => tuple.Item6,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        private static E GetItem<E>(in ValueTuple<E, E, E, E, E, E, E> tuple, int index)
        {
            switch (index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                case 2:
                    return tuple.Item3;
                case 3:
                    return tuple.Item4;
                case 4:
                    return tuple.Item5;
                case 5:
                    return tuple.Item6;
                case 6:
                    return tuple.Item7;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E, E, E, E, E, E> tuple, int index)
        {
            switch (index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                case 2:
                    return tuple.Item3;
                case 3:
                    return tuple.Item4;
                case 4:
                    return tuple.Item5;
                case 5:
                    return tuple.Item6;
                case 6:
                    return tuple.Item7;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, ValueTuple<T>> AsEnumerable<T>(this ValueTuple<T> tuple)
            => new EnumerableTuple<T, ValueTuple<T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T>> AsEnumerable<T>(this Tuple<T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T)> AsEnumerable<T>(this (T, T) tuple)
            => new EnumerableTuple<T, (T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T>> AsEnumerable<T>(this Tuple<T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T)> AsEnumerable<T>(this (T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T>> AsEnumerable<T>(this Tuple<T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T)> AsEnumerable<T>(this (T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T>>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T, T)>(tuple, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>>(tuple, GetItem);
    }
}