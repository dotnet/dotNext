using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public class LoopBuilder : ScopeBuilder
    {
        private static long loopCount = 0L;
        private protected readonly ScopeBuilder parentScope;
        private protected readonly LabelTarget breakLabel;
        private protected readonly LabelTarget continueLabel;

        internal LoopBuilder(ScopeBuilder parent)
        {
            parentScope = parent;
            var loopCount = LoopBuilder.loopCount.IncrementAndGet();
            breakLabel = Expression.Label("break_" + loopCount);
            continueLabel = Expression.Label("continue" + loopCount);

        }

        internal GotoExpression Continue() => Expression.Continue(continueLabel);

        internal GotoExpression Break() => Expression.Break(breakLabel);

        internal new LoopExpression BuildExpression()
            => base.BuildExpression().Loop(breakLabel, continueLabel);
    }
}