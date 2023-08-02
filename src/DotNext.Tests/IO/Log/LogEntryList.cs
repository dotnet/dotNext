using System.Collections;

namespace DotNext.IO.Log;

internal sealed class LogEntryList<TEntry, TEntryImpl, TList> : IReadOnlyList<TEntry>
    where TEntry : class, ILogEntry
    where TEntryImpl : notnull, TEntry
    where TList : notnull, IReadOnlyList<TEntryImpl>
{
    private sealed class Enumerator : Disposable, IEnumerator<TEntry>
    {
        private readonly IEnumerator<TEntryImpl> enumerator;

        internal Enumerator(TList list) => enumerator = list.GetEnumerator();

        public TEntry Current => enumerator.Current;

        object IEnumerator.Current => Current;

        public bool MoveNext() => enumerator.MoveNext();

        public void Reset() => enumerator.Reset();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                enumerator.Dispose();

            base.Dispose(disposing);
        }
    }

    // not readonly to avoid defensive copy
    private TList list;

    internal LogEntryList(TList list) => this.list = list;

    public TEntry this[int index] => list[index];

    public int Count => list.Count;

    public IEnumerator<TEntry> GetEnumerator() => new Enumerator(list);

    IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
}