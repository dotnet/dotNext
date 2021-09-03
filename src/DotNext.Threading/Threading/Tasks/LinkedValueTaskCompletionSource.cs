using System;

namespace DotNext.Threading.Tasks
{
    internal abstract class LinkedValueTaskCompletionSource<T, TNode> : ValueTaskCompletionSource<T>
        where TNode : LinkedValueTaskCompletionSource<T, TNode>
    {
        private readonly Action<TNode> backToPool;
        private TNode? previous, next;

        private protected LinkedValueTaskCompletionSource(Action<TNode> backToPool)
        {
            this.backToPool = backToPool;
        }

        internal TNode? Next => next;

        internal TNode? Previous => previous;

        internal bool IsNotRoot => next is not null || previous is not null;

        private protected abstract TNode CurrentNode { get; }

        internal void Append(TNode node)
        {
            next = node;
            node.previous = CurrentNode;
        }

        internal void Detach()
        {
            if (previous is not null)
                previous.next = next;
            if (next is not null)
                next.previous = previous;
            next = previous = null;
        }

        protected sealed override void AfterConsumed() => backToPool(CurrentNode);

        internal virtual TNode? CleanupAndGotoNext()
        {
            var next = this.next;
            this.next = previous = null;
            return next;
        }
    }
}