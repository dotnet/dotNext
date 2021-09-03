using System;
using Microsoft.Extensions.ObjectPool;

namespace DotNext.Threading.Tasks
{
    internal abstract class ValueTaskPool<T, TNode> : IPooledObjectPolicy<TNode>
        where TNode : LinkedValueTaskCompletionSource<T, TNode>
    {
        private readonly DefaultObjectPool<TNode> pool;
        private readonly Action<TNode> backToPool;

        protected ValueTaskPool(int concurrencyLevel)
        {
            pool = new(this, (concurrencyLevel / 2) + concurrencyLevel);
            this.backToPool = pool.Return;
        }

        internal TNode Get() => pool.Get();

        protected abstract TNode Create(Action<TNode> backToPool);

        TNode IPooledObjectPolicy<TNode>.Create() => Create(backToPool);

        bool IPooledObjectPolicy<TNode>.Return(TNode obj) => true;
    }
}