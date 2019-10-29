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
    public readonly struct EnumerableTuple<I, T> : IReadOnlyList<I>
        where T : IStructuralEquatable, IStructuralComparable
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
            private readonly int count;
            private int currentIndex;

            //TODO: in .NET Standard 2.1 parameter count can be replaced with ITuple.Length            
            internal Enumerator(T tuple, ItemAccessor accessor, int count)
            {
                this.tuple = tuple;
                currentIndex = -1;
                this.accessor = accessor;
                this.count = count;
            }

            /// <summary>
            /// Gets currently iterating item in the tuple.
            /// </summary>
            public I Current => accessor(in tuple, currentIndex);

            object IEnumerator.Current => Current;

            /// <summary>
            /// Advances position of this enumerator.
            /// </summary>
            /// <returns><see langword="true"/> if next item exists in the tuple; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext()
            {
                currentIndex += 1;
                return currentIndex < count;
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

        internal EnumerableTuple(T tuple, int count, ItemAccessor accessor)
        {
            this.tuple = tuple;
            Count = count;
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
        public int Count { get; }

        /// <summary>
        /// Gets enumerator over items in the tuple.
        /// </summary>
        /// <returns>The enumerator over items.</returns>
        public Enumerator GetEnumerator() => new Enumerator(tuple, accessor, Count);

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
        private static E GetItem<T, E>(in T tuple, int index)
            where T : struct, IStructuralEquatable, IStructuralComparable
        {
            var count = Unsafe.SizeOf<T>() / Unsafe.SizeOf<E>();
            return index >= 0 && index < count ?
                Unsafe.Add(ref Unsafe.As<T, E>(ref Unsafe.AsRef(in tuple)), index) :
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static E GetItem<E>(in Tuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static E GetItem<E>(in Tuple<E, E> tuple, int index)
        {
            switch (index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E, E> tuple, int index)
        {
            switch (index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                case 2:
                    return tuple.Item3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E, E, E> tuple, int index)
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E, E, E, E> tuple, int index)
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E, E, E, E, E> tuple, int index)
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
            => new EnumerableTuple<T, ValueTuple<T>>(tuple, 1, GetItem<ValueTuple<T>, T>);

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T>> AsEnumerable<T>(this Tuple<T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T>>(tuple, 1, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T)> AsEnumerable<T>(this (T, T) tuple)
            => new EnumerableTuple<T, (T, T)>(tuple, 2, GetItem<(T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T>> AsEnumerable<T>(this Tuple<T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T>>(tuple, 2, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T)> AsEnumerable<T>(this (T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T)>(tuple, 3, GetItem<(T, T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T>> AsEnumerable<T>(this Tuple<T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T>>(tuple, 3, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T)> AsEnumerable<T>(this (T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T)>(tuple, 4, GetItem<(T, T, T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T>>(tuple, 4, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T)>(tuple, 5, GetItem<(T, T, T, T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T>>(tuple, 5, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T)>(tuple, 6, GetItem<(T, T, T, T, T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T>>(tuple, 6, GetItem);

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T, T)>(tuple, 7, GetItem<(T, T, T, T, T, T, T), T>);

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>>(tuple, 7, GetItem);
    }
}