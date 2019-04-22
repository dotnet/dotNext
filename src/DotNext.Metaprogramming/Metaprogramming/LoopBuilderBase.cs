using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    /// <summary>
    /// Represents abstract class for loop statement builders.
    /// </summary>
    internal abstract class LoopBuilderBase : LexicalScope
    {
        private static long loopCount = 0L;
        internal protected readonly LabelTarget BreakLabel;
        internal protected readonly LabelTarget ContinueLabel;

        private protected LoopBuilderBase(LexicalScope parent)
            : base(parent)
        {
            var loopCount = LoopBuilderBase.loopCount.IncrementAndGet();
            BreakLabel = Expression.Label(typeof(void), "break_" + loopCount);
            ContinueLabel = Expression.Label(typeof(void), "continue" + loopCount);
        }
    }
}