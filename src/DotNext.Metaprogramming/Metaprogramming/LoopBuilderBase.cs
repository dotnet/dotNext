using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    /// <summary>
    /// Represents abstract class for loop statement builders.
    /// </summary>
    public abstract class LoopBuilderBase : ScopeBuilder
    {
        private static long loopCount = 0L;
        private protected readonly LabelTarget breakLabel;
        private protected readonly LabelTarget continueLabel;

        private protected LoopBuilderBase(CompoundStatementBuilder parent)
            : base(parent)
        {
            var loopCount = LoopBuilderBase.loopCount.IncrementAndGet();
            breakLabel = Expression.Label("break_" + loopCount);
            continueLabel = Expression.Label("continue" + loopCount);
        }

        /// <summary>
        /// Restarts execution of this loop.
        /// </summary>
        /// <returns>An expression representing jump to the beginning of the loop.</returns>
        public GotoExpression Continue() => Continue(true);

        /// <summary>
        /// Stops execution of this loop.
        /// </summary>
        /// <returns>An expression representing jump outside of the loop.</returns>
        public GotoExpression Break() => Break(true);

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