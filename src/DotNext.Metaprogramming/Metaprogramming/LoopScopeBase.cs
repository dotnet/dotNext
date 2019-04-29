using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents abstract class for loop statement builders.
    /// </summary>
    internal abstract class LoopScopeBase : LexicalScope
    {
        internal abstract LabelTarget BreakLabel { get; }
        internal abstract LabelTarget ContinueLabel { get; }

        private protected LoopScopeBase(LexicalScope parent)
            : base(parent)
        {
        }
    }
}