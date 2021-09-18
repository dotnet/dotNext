using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

internal sealed class LoopStatement : LoopLexicalScope, ILexicalScope<LoopExpression, Action>, ILexicalScope<LoopExpression, Action<LoopContext>>
{
    internal LoopStatement()
    {
    }

    LoopExpression ILexicalScope<LoopExpression, Action>.Build(Action scope)
    {
        scope();
        return Expression.Loop(Build(), BreakLabel, ContinueLabel);
    }

    LoopExpression ILexicalScope<LoopExpression, Action<LoopContext>>.Build(Action<LoopContext> scope)
    {
        using (var context = new LoopContext(this))
            scope(context);
        return Expression.Loop(Build(), BreakLabel, ContinueLabel);
    }
}