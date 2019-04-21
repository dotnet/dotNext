using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DotNext.Metaprogramming
{
    using Threading;

    /// <summary>
    /// Identifies loop.
    /// </summary>
    /// <remarks>
    /// This type can be used to transfer control between outer and inner loops.
    /// </remarks>
    public readonly struct LoopCookie: IDisposable
    {
        private readonly WeakReference<LoopBuilderBase> scope;

        internal LoopCookie(LoopBuilderBase scope) => this.scope = new WeakReference<LoopBuilderBase>(scope);

        internal bool TryGetScope(out LoopBuilderBase scope) => this.scope.TryGetTarget(out scope);

        void IDisposable.Dispose() => scope.SetTarget(null);
    }

    /// <summary>
    /// Represents abstract class for loop statement builders.
    /// </summary>
    internal abstract class LoopBuilderBase : ScopeBuilder
    {
        private static long loopCount = 0L;
        private protected readonly LabelTarget breakLabel;
        private protected readonly LabelTarget continueLabel;

        private protected LoopBuilderBase(LexicalScope parent)
            : base(parent)
        {
            var loopCount = LoopBuilderBase.loopCount.IncrementAndGet();
            breakLabel = Expression.Label("break_" + loopCount);
            continueLabel = Expression.Label("continue" + loopCount);
        }

        /// <summary>
        /// Restarts execution of this loop.
        /// </summary>
        public void Continue() => Continue(true);

        /// <summary>
        /// Stops execution of this loop.
        /// </summary>
        public void Break() => Break(true);

        internal GotoExpression Continue(bool addAsStatement)
        {
            var expr = Expression.Continue(continueLabel);
            if(addAsStatement)
                AddStatement(expr);
            return expr;
        }

        internal GotoExpression Break(bool addAsStatement)
        {
            var expr = Expression.Break(breakLabel);
            if(addAsStatement)
                AddStatement(expr);
            return expr;
        }
    }
}