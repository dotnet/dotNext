using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ILoopExpression = Linq.Expressions.ILoopExpression;

    internal abstract class LoopLexicalScope : LexicalScope, ILoopExpression
    {
        private protected LoopLexicalScope(LexicalScope parent)
            : base(parent)
        {
            ContinueLabel = Expression.Label(typeof(void), "continue");
            BreakLabel = Expression.Label(typeof(void), "break");
        }

        public LabelTarget ContinueLabel { get; }

        public LabelTarget BreakLabel { get; }
    }
}
