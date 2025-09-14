using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks.Pooling;

/*
 * Represents a pool without any allocations. Assuming that wait queues are organized using
 * linked nodes where each node is a completion source. If so, the node pointers (next/previous)
 * can be used to keep completion sources in the pool. The access must be synchronized.
 */
[StructLayout(LayoutKind.Auto)]
internal struct ValueTaskPool<T>(long maximumRetained)
{
    private LinkedValueTaskCompletionSource<T>? first;
    private long count;

    public ValueTaskPool()
        : this(long.MaxValue)
    {
    }

    public readonly long MaximumRetained => maximumRetained;

    public void Return(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(node is { Status: ManualResetCompletionSourceStatus.WaitForActivation });
        Debug.Assert(node is { Next: null, Previous: null });

        if (count < maximumRetained)
        {
            node.Next = first;
            first = node;
            count++;
        }
    }

    public TNode Rent<TNode>()
        where TNode : LinkedValueTaskCompletionSource<T>, new()
    {
        if (first is TNode result)
        {
            first = result.Next;
            result.Next = null;
            count--;

            Debug.Assert(count >= 0L);
            Debug.Assert(count is 0L || first is not null);
        }
        else
        {
            Debug.Assert(count is 0L);

            result = new();
        }

        return result;
    }
}