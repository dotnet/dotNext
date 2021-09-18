using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using ILoopLabels = Linq.Expressions.ILoopLabels;

internal abstract class LoopLexicalScope : Statement, ILoopLabels
{
    private protected LoopLexicalScope()
    {
        ContinueLabel = Expression.Label(typeof(void), "continue");
        BreakLabel = Expression.Label(typeof(void), "break");
    }

    public LabelTarget ContinueLabel { get; }

    public LabelTarget BreakLabel { get; }
}