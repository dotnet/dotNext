using System.Collections.Concurrent;

namespace DotNext.Threading.Tasks.Pooling;

internal sealed class UnconstrainedValueTaskPool<TNode> : ConcurrentBag<TNode>, ISupplier<TNode>, IConsumer<TNode>
    where TNode : ManualResetCompletionSource, IPooledManualResetCompletionSource<TNode>
{
    private readonly Action<TNode> backToPool;

    internal UnconstrainedValueTaskPool()
        => backToPool = new WeakReference<UnconstrainedValueTaskPool<TNode>?>(this, false).Consume;

    TNode ISupplier<TNode>.Invoke()
    {
        if (!TryTake(out var result))
            result = TNode.CreateSource(backToPool);

        return result;
    }

    void IConsumer<TNode>.Invoke(TNode node)
    {
        node.Reset();
        Add(node);
    }
}