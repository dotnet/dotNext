using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    public sealed class SingleList<T>: Tuple<T>, IReadOnlyList<T>
    {
        public struct Enumerator : IEnumerator<T>
        {
            private bool requested;

            internal Enumerator(T element)
            {
                Current = element;
                requested = false;
            }

            public T Current { get; }

            object IEnumerator.Current => Current;

            void IDisposable.Dispose()
            {
            }

            public bool MoveNext()
                => requested ? false : requested = true;

            public void Reset() => requested = false;
        }

        public SingleList(T item)
            : base(item)
        {
        }

        T IReadOnlyList<T>.this[int index] 
            => index == 0 ? Item1 : throw new IndexOutOfRangeException(ExceptionMessages.IndexShouldBeZero);

        int IReadOnlyCollection<T>.Count => 1;

        public Enumerator GetEnumerator() => new Enumerator(Item1);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
    }
}
