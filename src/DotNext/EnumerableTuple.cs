using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext
{
    using static Runtime.Intrinsics;

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

        /// <summary>
        /// Represents enumerator over items in the tuple.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<I>
        {
            private const int InitialPosition = -1;
            private T tuple;
            private readonly ValueRefFunc<T, int, I> accessor;
            private readonly int count;
            private int currentIndex;

            //TODO: in .NET Standard 2.1 parameter count can be replaced with ITuple.Length            
            internal Enumerator(T tuple, in ValueRefFunc<T, int, I> accessor, int count)
            {
                this.tuple = tuple;
                currentIndex = InitialPosition;
                this.accessor = accessor;
                this.count = count;
            }

            /// <summary>
            /// Gets currently iterating item in the tuple.
            /// </summary>
            public I Current => accessor.Invoke(ref tuple, currentIndex);

            object IEnumerator.Current => Current;

            /// <summary>
            /// Advances position of this enumerator.
            /// </summary>
            /// <returns><see langword="true"/> if next item exists in the tuple; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext() => ++currentIndex < count;

            /// <summary>
            /// Sets the enumerator to its initial position, which is before 
            /// the first item in the tuple.
            /// </summary>
            public void Reset() => currentIndex = InitialPosition;

            void IDisposable.Dispose() => this = default;
        }

        private readonly T tuple;
        private readonly ValueRefFunc<T, int, I> accessor;

        internal EnumerableTuple(T tuple, int count, in ValueRefFunc<T, int, I> accessor)
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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is invalid.</exception>
        public I this[int index] => accessor.IsEmpty ? throw new ArgumentOutOfRangeException(nameof(index)) : accessor.Invoke(ref AsRef(tuple), index);

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
        private static E GetItem<E>(ref Tuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static E GetItem<E>(ref Tuple<E, E> tuple, int index)
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

        private static E GetItem<E>(ref Tuple<E, E, E> tuple, int index)
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

        private static E GetItem<E>(ref Tuple<E, E, E, E> tuple, int index)
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

        private static E GetItem<E>(ref Tuple<E, E, E, E, E> tuple, int index)
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

        private static E GetItem<E>(ref Tuple<E, E, E, E, E, E> tuple, int index)
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

        private static E GetItem<E>(ref Tuple<E, E, E, E, E, E, E> tuple, int index)
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
            => new EnumerableTuple<T, ValueTuple<T>>(tuple, 1, new ValueRefFunc<ValueTuple<T>, int, T>(GetTupleItem<ValueTuple<T>, T>));

        /// <summary>
        /// Converts tuple into enumerable collection of single item.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T>> AsEnumerable<T>(this Tuple<T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T>>(tuple, 1, new ValueRefFunc<Tuple<T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T)> AsEnumerable<T>(this (T, T) tuple)
            => new EnumerableTuple<T, (T, T)>(tuple, 2, new ValueRefFunc<(T, T), int, T>(GetTupleItem<(T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of two items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T>> AsEnumerable<T>(this Tuple<T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T>>(tuple, 2, new ValueRefFunc<Tuple<T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T)> AsEnumerable<T>(this (T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T)>(tuple, 3, new ValueRefFunc<(T, T, T), int, T>(GetTupleItem<(T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of three items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T>> AsEnumerable<T>(this Tuple<T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T>>(tuple, 3, new ValueRefFunc<Tuple<T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T)> AsEnumerable<T>(this (T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T)>(tuple, 4, new ValueRefFunc<(T, T, T, T), int, T>(GetTupleItem<(T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of four items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T>>(tuple, 4, new ValueRefFunc<Tuple<T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T)>(tuple, 5, new ValueRefFunc<(T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of five items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T>>(tuple, 5, new ValueRefFunc<Tuple<T, T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T)>(tuple, 6, new ValueRefFunc<(T, T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of six items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T>>(tuple, 6, new ValueRefFunc<Tuple<T, T, T, T, T, T>, int, T>(GetItem));

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, (T, T, T, T, T, T, T)> AsEnumerable<T>(this (T, T, T, T, T, T, T) tuple)
            => new EnumerableTuple<T, (T, T, T, T, T, T, T)>(tuple, 7, new ValueRefFunc<(T, T, T, T, T, T, T), int, T>(GetTupleItem<(T, T, T, T, T, T, T), T>));

        /// <summary>
        /// Converts tuple into enumerable collection of seven items.
        /// </summary>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <param name="tuple">The tuple to be converted into enumerable collection.</param>
        /// <returns>The tuple in the form of enumerable collection.</returns>
        public static EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>> AsEnumerable<T>(this Tuple<T, T, T, T, T, T, T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T, T, T, T, T, T, T>>(tuple, 7, new ValueRefFunc<Tuple<T, T, T, T, T, T, T>, int, T>(GetItem));
    }
}