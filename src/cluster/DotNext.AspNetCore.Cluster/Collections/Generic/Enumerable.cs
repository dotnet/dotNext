using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

[StructLayout(LayoutKind.Auto)]
internal struct Enumerable<T, TList> : IEnumerable<T>
    where TList : IReadOnlyList<T>
{
    [StructLayout(LayoutKind.Auto)]
    internal struct Enumerator : IEnumerator<T>
    {
        private const int InitialIndex = -1;
        private int index;
        private TList list;

        internal Enumerator(TList list)
        {
            this.list = list;
            index = InitialIndex;
        }

        public bool MoveNext() => ++index < list.Count;

        public T Current => list[index];

        object? IEnumerator.Current => Current;

        void IEnumerator.Reset() => index = InitialIndex;

        public void Dispose() => this = default;
    }

    // not readonly to avoid defensive copying
    private TList list;

    internal Enumerable(in TList list) => this.list = list;

    internal int Count => list.Count;

    public readonly Enumerator GetEnumerator() => new(list);

    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => list.Count == 0 ? Sequence.GetEmptyEnumerator<T>() : GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}