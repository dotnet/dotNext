using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public abstract class LoopBuilderBase : ExpressionBuilder
    {
        private static long loopCount = 0L;
        private protected readonly LabelTarget breakLabel;
        private protected readonly LabelTarget continueLabel;

        private protected LoopBuilderBase(ExpressionBuilder parent)
            : base(parent)
        {
            var loopCount = LoopBuilderBase.loopCount.IncrementAndGet();
            breakLabel = Expression.Label("break_" + loopCount);
            continueLabel = Expression.Label("continue" + loopCount);
        }

        internal GotoExpression Continue() => Expression.Continue(continueLabel);

        internal GotoExpression Break() => Expression.Break(breakLabel);
    }
}