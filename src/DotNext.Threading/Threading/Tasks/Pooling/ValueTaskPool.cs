using System.Reflection;
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
internal struct ValueTaskPool<T, TNode>
    where TNode : LinkedValueTaskCompletionSource<T>, IPooledManualResetCompletionSource<TNode>, new()
{
    private readonly long maximumRetained; // zero when no limitations
    private readonly Action<TNode> backToPool;
    private TNode? first;
    private long count;

    internal ValueTaskPool(Action<TNode> backToPool, long? maximumRetained = null)
    {
        Debug.Assert(backToPool is not null);
        Debug.Assert((backToPool.Method.MethodImplementationFlags & MethodImplAttributes.Synchronized) != 0);

        this.backToPool = backToPool;
        first = null;
        count = 0;
        this.maximumRetained = maximumRetained.GetValueOrDefault(0L);
    }

    internal void Return(TNode node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(backToPool.Target is not null);
        Debug.Assert(Monitor.IsEntered(backToPool.Target));

        if (!node.TryReset(out _))
            goto exit;

        node.OnConsumed = null;

        if (maximumRetained > 0L && count >= maximumRetained)
            goto exit;

        Debug.Assert(count == 0L || first is not null);

        first?.Prepend(node);
        first = node;
        count++;
        Debug.Assert(count > 0L);

    exit:
        return;
    }

    internal TNode Get()
    {
        Debug.Assert(backToPool.Target is not null);
        Debug.Assert(Monitor.IsEntered(backToPool.Target));

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
            Debug.Assert(count == 0L || first is not null);
        }

        result.OnConsumed = backToPool;
        return result;
    }
}