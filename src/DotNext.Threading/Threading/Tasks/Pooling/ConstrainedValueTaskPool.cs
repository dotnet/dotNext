using Microsoft.Extensions.ObjectPool;

namespace DotNext.Threading.Tasks.Pooling;

internal sealed class ConstrainedValueTaskPool<TNode> : DefaultObjectPool<TNode>, ISupplier<TNode>, IConsumer<TNode>
    where TNode : ManualResetCompletionSource, IPooledManualResetCompletionSource<TNode>
{
    private sealed class PooledNodePolicy : IPooledObjectPolicy<TNode>
    {
        private readonly Action<TNode> backToPool;

        internal PooledNodePolicy(Action<TNode> backToPool) => this.backToPool = backToPool;

        TNode IPooledObjectPolicy<TNode>.Create() => TNode.CreateSource(backToPool);

        bool IPooledObjectPolicy<TNode>.Return(TNode obj)
        {
            obj.Reset();
            return true;
        }
    }

    internal ConstrainedValueTaskPool(int maximumRetained)
        : base(new PooledNodePolicy(CreateBackToPoolCallback(out var weakRef)), maximumRetained)
        => weakRef.SetTarget(this);

    private static Action<TNode> CreateBackToPoolCallback(out WeakReference<ConstrainedValueTaskPool<TNode>?> weakRef)
    {
        weakRef = new(null, false);
        return weakRef.Consume;
    }

    TNode ISupplier<TNode>.Invoke() => Get();

    void IConsumer<TNode>.Invoke(TNode node) => Return(node);
}