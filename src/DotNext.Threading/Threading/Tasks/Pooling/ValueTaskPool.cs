using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks.Pooling;

/*
 * Represents a pool without any allocations. Assuming that wait queues are organized using
 * linked nodes where each node is a completion source. If so, the node pointers (next/previous)
 * can be used to keep completion sources in the pool. Moreover, access to the pool is synchronized
 * by the caller (see QueuedSynchronizer). Thus, it doesn't require explicit synchronization such as
 * Monitor lock.
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
        Debug.Assert(node is not null);
        Debug.Assert(count is 0L || first is not null);
        Debug.Assert(node.Status is ManualResetCompletionSourceStatus.WaitForActivation);

        if (count < maximumRetained)
        {
            first?.Prepend(node);
            first = node;
            count++;
            Debug.Assert(count > 0L);
        }
    }

    public TNode Get<TNode>()
        where TNode : LinkedValueTaskCompletionSource<T>, new()
    {
        TNode result;
        if (first is null)
        {
            Debug.Assert(count == 0L);

            result = new();
        }
        else
        {
            Debug.Assert(first is TNode);
            result = Unsafe.As<TNode>(first);
            first = result.Next;
            result.Detach();
            count--;

            Debug.Assert(count >= 0L);
            Debug.Assert(count is 0L || first is not null);
        }

        return result;
    }
}