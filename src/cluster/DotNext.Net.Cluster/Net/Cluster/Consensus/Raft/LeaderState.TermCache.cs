using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft;

using List = Collections.Generic.List;

internal partial class LeaderState<TMember>
{
    // it behaves like SortedList but allows to remove multiple keys in batch manner
    // key is log entry index, value is log entry term
    internal sealed class TermCache : List<KeyValuePair<long, long>>
    {
        private sealed class IndexComparer : IComparer<KeyValuePair<long, long>>
        {
            internal static readonly IndexComparer Instance = new();

            private IndexComparer()
            {
            }

            int IComparer<KeyValuePair<long, long>>.Compare(KeyValuePair<long, long> x, KeyValuePair<long, long> y)
                => x.Key.CompareTo(y.Key);
        }

        internal TermCache(int capacity)
            : base(capacity)
        {
        }

        internal void Add(long index, long term)
            => List.InsertOrdered(this, new(index, term), IndexComparer.Instance);

        private int Find(long index)
            => BinarySearch(new(index, 0L), IndexComparer.Instance);

        internal void RemoveHead(long index)
        {
            var i = Find(index);
            if (i >= 0)
                RemoveRange(0, i);
        }

        internal bool TryGetValue(long index, out long term)
        {
            var i = Find(index);
            if (i >= 0)
            {
                term = Unsafe.Add(ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(this)), i).Value;
                return true;
            }

            term = default;
            return false;
        }
    }

    // key is log entry index, value is log entry term
    private readonly TermCache precedingTermCache;
}