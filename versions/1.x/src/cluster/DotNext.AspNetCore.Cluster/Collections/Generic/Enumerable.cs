using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct Enumerable<T, TList>
        where TList : IReadOnlyList<T>
    {
        [StructLayout(LayoutKind.Auto)]
        internal struct Enumerator
        {
            private const int InitialIndex = -1;
            private int index;
            private readonly TList list;

            internal Enumerator(TList list)
            {
                this.list = list;
                index = InitialIndex;
            }

            public bool MoveNext() => ++index < list.Count;

            public T Current => list[index];
        }

        private readonly TList list;

        internal Enumerable(TList list) => this.list = list;

        internal int Count => list.Count;

        public Enumerator GetEnumerator() => new Enumerator(list);
    }
}