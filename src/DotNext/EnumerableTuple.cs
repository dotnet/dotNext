using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    public readonly struct EnumerableTuple<E, T> : IEnumerable<E>
        where T : IStructuralEquatable, IStructuralComparable //, ITuple
    {
        internal delegate E ItemAccessor(in T tuple, int index);

        public struct Enumerator : IEnumerator<E>
        {
            private readonly T tuple;
            private readonly ItemAccessor accessor;
            private readonly int count;
            private int currentIndex;

            //in .NET Standard 2.1 parameter count can be replaced with ITuple.Length            
            internal Enumerator(T tuple, ItemAccessor accessor, int count)
            {
                this.tuple = tuple;
                currentIndex = -1;
                this.accessor = accessor;
                this.count = count;
            }

            public E Current => accessor(in tuple, currentIndex);

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                currentIndex += 1;
                return currentIndex < count;
            }

            public void Reset() => currentIndex = -1;

            void IDisposable.Dispose() => this = default;
        }

        private readonly T tuple;
        private readonly int count;
        private readonly ItemAccessor accessor;

        internal EnumerableTuple(T tuple, int count, ItemAccessor accessor)
        {
            this.tuple = tuple;
            this.count = count;
            this.accessor = accessor;
        }

        public Enumerator GetEnumerator() => new Enumerator(tuple, accessor, count);

        IEnumerator<E> IEnumerable<E>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class EnumerableTuple
    {
        private static E GetItem<E>(in ValueTuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));
        
        private static E GetItem<E>(in Tuple<E> tuple, int index)
            => index == 0 ? tuple.Item1 : throw new ArgumentOutOfRangeException(nameof(index));

        private static E GetItem<E>(in ValueTuple<E, E> tuple, int index)
        {
            switch(index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static E GetItem<E>(in Tuple<E, E> tuple, int index)
        {
            switch(index)
            {
                case 0:
                    return tuple.Item1;
                case 1:
                    return tuple.Item2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static EnumerableTuple<T, ValueTuple<T>> AsEnumerable<T>(this ValueTuple<T> tuple)
            => new EnumerableTuple<T, ValueTuple<T>>(tuple, 1, GetItem);
        
        public static EnumerableTuple<T, Tuple<T>> AsEnumerable<T>(this Tuple<T> tuple)
            => tuple is null ? default : new EnumerableTuple<T, Tuple<T>>(tuple, 1, GetItem);

        public static EnumerableTuple<T, (T, T)> AsEnumerable<T>(this (T, T) tuple)
            => new EnumerableTuple<T, (T, T)>(tuple, 2, GetItem);

        public static EnumerableTuple<T, Tuple<T, T>> AsEnumerable<T>(this Tuple<T, T> tuple)
            => new EnumerableTuple<T, Tuple<T, T>>(tuple, 2, GetItem);
    }
}