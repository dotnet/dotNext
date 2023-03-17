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
internal struct ValueTaskPool<T, TNode, TCallback>
    where TNode : LinkedValueTaskCompletionSource<T>, IPooledManualResetCompletionSource<TCallback>, new()
    where TCallback : MulticastDelegate
{
    private readonly long maximumRetained;
    private readonly TCallback backToPool;
    private TNode? first;
    private long count;

    internal ValueTaskPool(TCallback backToPool, long? maximumRetained = null)
    {
        Debug.Assert(backToPool is not null);

        this.backToPool = backToPool;
        first = null;
        count = 0;
        this.maximumRetained = maximumRetained ?? long.MaxValue;
    }

    internal void Return(TNode node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(backToPool.Target is not null);
        Debug.Assert(count is 0L || first is not null);

        if (!node.TryReset(out _))
            return;

        node.OnConsumed = null;

        if (count < maximumRetained)
        {
            first?.Prepend(node);
            first = node;
            count++;
            Debug.Assert(count > 0L);
        }
    }

    internal TNode Get()
    {
        Debug.Assert(backToPool.Target is not null);

        TNode result;
        if (first is null)
        {
            Debug.Assert(count == 0L);

            result = new();
        }
        else
        {
            result = first;
            first = result.Next as TNode;
            result.Detach();
            count--;

            Debug.Assert(count >= 0L);
            Debug.Assert(count is 0L || first is not null);
        }

        result.OnConsumed = backToPool;
        return result;
    }
}