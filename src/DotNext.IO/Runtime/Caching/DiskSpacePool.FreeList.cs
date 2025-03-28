using System.Runtime.CompilerServices;

namespace DotNext.Runtime.Caching;

public partial class DiskSpacePool
{
    private volatile SegmentHandle? freeList;
    private long cursor;

    private long AllocateSegment() => Interlocked.Add(ref cursor, MaxSegmentSize);
    
    private SegmentHandle RentOffset()
    {
        SegmentHandle result;
        for (SegmentHandle? headCopy = freeList, tmp;; headCopy = tmp)
        {
            if (headCopy is null)
            {
                result = new(this);
                break;
            }

            tmp = headCopy.TryGetNext(out var next)
                ? Interlocked.CompareExchange(ref freeList, next, headCopy)
                : freeList;
            
            if (ReferenceEquals(tmp, headCopy))
            {
                tmp.MoveToCompletedState(this);
                result = tmp;
                break;
            }
        }

        return result;
    }

    private void ReturnOffset(long offset)
    {
        var node = new SegmentHandle(offset);
        for (SegmentHandle? headCopy = freeList, tmp;; headCopy = tmp)
        {
            node.SetNext(headCopy);

            tmp = Interlocked.CompareExchange(ref freeList, node, headCopy);
            if (ReferenceEquals(tmp, headCopy))
                break;
        }
    }
    
    internal sealed class SegmentHandle(long offset)
    {
        // Can be null, or Node, or DiskSpacePool, or Sentinel. Null treats as null Node.
        // Every type represents a state of the node.
        // Possible transitions:
        // null or Node => DiskSpacePool - taken from the free list
        // DiskSpacePool => Sentinel - disposed
        private object? ownerOrNext;

        internal SegmentHandle(DiskSpacePool pool)
            : this(pool.AllocateSegment())
            => ownerOrNext = pool;

        internal bool TryGetNext(out SegmentHandle? next)
        {
            var ownerOrNextCopy = ownerOrNext;
            bool result;
            next = (result = ownerOrNextCopy is null or SegmentHandle)
                ? Unsafe.As<SegmentHandle?>(ownerOrNextCopy)
                : null;

            return result;
        }

        internal void SetNext(SegmentHandle? next) => ownerOrNext = next;

        internal void MoveToCompletedState(DiskSpacePool pool) => ownerOrNext = pool;

        internal DiskSpacePool? TryGetOwner() => ReferenceEquals(ownerOrNext, Sentinel.Instance) ? null : Unsafe.As<DiskSpacePool?>(ownerOrNext);

        internal void MoveToDisposedState() => ownerOrNext = Sentinel.Instance;
        
        internal long Offset => offset;

        public override string ToString() => offset.ToString();
    }
}