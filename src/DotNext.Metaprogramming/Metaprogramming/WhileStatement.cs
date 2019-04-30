using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using DotNext.Linq.Expressions;
    using WhileExpression = Linq.Expressions.WhileExpression;

    internal readonly struct WhileStatement : IStatement<WhileExpression, Action>, IStatement<WhileExpression, Action<LoopContext>>
    {
        private readonly Expression condition;
        private readonly bool conditionFirst;

        internal WhileStatement(Expression condition, bool conditionFirst)
        {
            this.condition = condition;
            this.conditionFirst = conditionFirst;
        }

        WhileExpression IStatement<WhileExpression, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return new WhileExpression(condition, body.Build(), conditionFirst);
        }

        WhileExpression IStatement<WhileExpression, Action<LoopContext>>.Build(Action<LoopContext> scope, ILexicalScope body)
        {
            var result = new WhileExpression(condition, checkConditionFirst: conditionFirst);
            scope(new LoopContext(result));
            result.Body = body.Build();
            return result;
        }
    }
}