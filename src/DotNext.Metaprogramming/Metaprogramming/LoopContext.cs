using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    /// <summary>
    /// Identifies loop.
    /// </summary>
    /// <remarks>
    /// This type can be used to transfer control between outer and inner loops.
    /// </remarks>
    public readonly struct LoopContext
    {
        internal readonly LabelTarget ContinueLabel, BreakLabel;

        internal LoopContext(LabelTarget @continue, LabelTarget @break)
        {
            ContinueLabel = @continue;
            BreakLabel = @break;
        }

        internal LoopContext(ILoopExpression loop)
        {
            ContinueLabel = loop.ContinueLabel;
            BreakLabel = loop.BreakLabel;
        }
    }
}
