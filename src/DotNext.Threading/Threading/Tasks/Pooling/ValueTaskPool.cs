using Microsoft.Extensions.ObjectPool;

namespace DotNext.Threading.Tasks.Pooling;

internal sealed class ValueTaskPool<TNode> : DefaultObjectPool<TNode>
    where TNode : ManualResetCompletionSource, IPooledManualResetCompletionSource<TNode>, new()
{
    private sealed class PooledNodePolicy : IPooledObjectPolicy<TNode>
    {
        private readonly Action<TNode> consumedCallback;

        internal PooledNodePolicy(Action<TNode> consumedCallback)
            => this.consumedCallback = consumedCallback;

        TNode IPooledObjectPolicy<TNode>.Create()
        {
            var result = new TNode();
            result.OnConsumed = consumedCallback;
            return result;
        }

        bool IPooledObjectPolicy<TNode>.Return(TNode obj) => obj.TryReset(out _);
    }

    private sealed class ValueTaskPoolWeakReference : WeakReference
    {
        internal ValueTaskPoolWeakReference()
            : base(null, false)
        {
        }

        internal new ValueTaskPool<TNode>? Target
        {
            get => base.Target as ValueTaskPool<TNode>;
            set => base.Target = value;
        }

        internal void Return(TNode node) => Target?.Return(node);
    }

    internal ValueTaskPool(int maximumRetained, Action<TNode>? completionCallback = null)
        : base(new PooledNodePolicy(completionCallback + CreateBackToPoolCallback(out var weakRef)), maximumRetained)
        => weakRef.Target = this;

    internal ValueTaskPool(Action<TNode>? completionCallback = null)
        : base(new PooledNodePolicy(completionCallback + CreateBackToPoolCallback(out var weakRef)))
        => weakRef.Target = this;

    private static Action<TNode> CreateBackToPoolCallback(out ValueTaskPoolWeakReference weakRef)
        => (weakRef = new()).Return;
}