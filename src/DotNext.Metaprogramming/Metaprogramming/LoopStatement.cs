using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class LoopStatement : IStatement<LoopExpression, Action>, IStatement<LoopExpression, Action<LoopContext>>
    {
        internal static readonly LoopStatement Instance = new LoopStatement();

        private LoopStatement() { }

        LoopExpression IStatement<LoopExpression, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return Expression.Loop(body.Build());
        }

        LoopExpression IStatement<LoopExpression, Action<LoopContext>>.Build(Action<LoopContext> scope, ILexicalScope body)
        {
            var context = new LoopContext(Expression.Label("continue"), Expression.Label("break"));
            scope(context);
            return Expression.Loop(body.Build(), context.BreakLabel, context.ContinueLabel);
        }
    }
}