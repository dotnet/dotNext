namespace DotNext.Runtime.Caching;

public partial class DiskSpacePool
{
    private volatile Node? head;
    private long cursor;
    
    private long RentOffset()
    {
        long offset;
        for (Node? headCopy = head, tmp;; headCopy = tmp)
        {
            if (headCopy is null)
            {
                offset = Interlocked.Add(ref cursor, MaxSegmentSize);
                break;
            }

            tmp = Interlocked.CompareExchange(ref head, headCopy.Next, headCopy);
            if (ReferenceEquals(tmp, headCopy))
            {
                offset = tmp.Offset;
                break;
            }
        }

        return offset;
    }

    private void ReturnOffset(long offset)
    {
        var node = new Node(offset);

        for (Node? headCopy = head, tmp;; headCopy = tmp)
        {
            node.Next = headCopy;

            tmp = Interlocked.CompareExchange(ref head, node, headCopy);
            if (ReferenceEquals(tmp, headCopy))
                break;
        }
    }
    
    private sealed class Node(long offset)
    {
        internal Node? Next;
        internal long Offset => offset;
    }
}