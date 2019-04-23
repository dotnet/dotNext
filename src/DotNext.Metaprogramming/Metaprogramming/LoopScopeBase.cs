using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    /// <summary>
    /// Represents abstract class for loop statement builders.
    /// </summary>
    internal abstract class LoopScopeBase : LexicalScope
    {
        private static long loopCount = 0L;
        internal protected readonly LabelTarget BreakLabel;
        internal protected readonly LabelTarget ContinueLabel;

        private protected LoopScopeBase(LexicalScope parent)
            : base(parent)
        {
            var loopCount = LoopScopeBase.loopCount.IncrementAndGet();
            BreakLabel = Expression.Label(typeof(void), "break_" + loopCount);
            ContinueLabel = Expression.Label(typeof(void), "continue" + loopCount);
        }
    }
}