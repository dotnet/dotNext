using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public abstract class LoopBuilderBase : ScopeBuilder
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

        public GotoExpression Continue() => Continue(true);

        public GotoExpression Break() => Break(true);

        internal GotoExpression Continue(bool addAsStatement)
        {
            var expr = Expression.Continue(continueLabel);
            return addAsStatement ? AddStatement(expr) : expr;
        }

        internal GotoExpression Break(bool addAsStatement)
        {
            var expr = Expression.Break(breakLabel);
            return addAsStatement ? AddStatement(expr) : expr;
        }
    }
}