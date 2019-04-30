using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using WhileExpression = Linq.Expressions.WhileExpression;

    internal sealed class WhileStatement : LoopLexicalScope, ILexicalScope<WhileExpression, Action>, ILexicalScope<WhileExpression, Action<LoopContext>>
    {
        private readonly Expression condition;
        private readonly bool conditionFirst;

        internal WhileStatement(Expression condition, bool conditionFirst, LexicalScope parent)
            : base(parent)
        {
            this.condition = condition;
            this.conditionFirst = conditionFirst;
        }

        WhileExpression ILexicalScope<WhileExpression, Action>.Build(Action scope)
        {
            scope();
            return new WhileExpression(condition, Build(), conditionFirst);
        }

        WhileExpression ILexicalScope<WhileExpression, Action<LoopContext>>.Build(Action<LoopContext> scope)
        {
            var result = new WhileExpression(condition, checkConditionFirst: conditionFirst);
            scope(new LoopContext(result));
            result.Body = Build();
            return result;
        }
    }
}