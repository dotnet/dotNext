using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Tasks;

partial class QueuedSynchronizer
{
    private readonly long maximumRetained;
    private LinkedValueTaskCompletionSource<bool>? firstInPool;
    private long poolSize;

    private void BackToPool(LinkedValueTaskCompletionSource<bool> node)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));
        Debug.Assert(node is not null);
        Debug.Assert(poolSize is 0L || firstInPool is not null);

        if (!node.TryReset(out _))
            return;

        if (poolSize < maximumRetained)
        {
            firstInPool?.Prepend(node);
            firstInPool = node;
            poolSize++;
            Debug.Assert(poolSize > 0L);
        }
    }
    
    private TNode RentFromPool<TNode>()
        where TNode : LinkedValueTaskCompletionSource<bool>, new()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        TNode result;
        if (firstInPool is null)
        {
            Debug.Assert(poolSize is 0L);

            result = new();
        }
        else
        {
            Debug.Assert(firstInPool is TNode);
            
            result = Unsafe.As<TNode>(firstInPool);
            firstInPool = result.Next;
            result.Detach();
            poolSize--;

            Debug.Assert(poolSize >= 0L);
            Debug.Assert(poolSize is 0L || firstInPool is not null);
        }

        return result;
    }
}