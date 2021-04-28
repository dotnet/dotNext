using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using WhileExpression = Linq.Expressions.WhileExpression;

    internal sealed class WhileStatement : LoopLexicalScope, ILexicalScope<WhileExpression, Action>, ILexicalScope<WhileExpression, Action<LoopContext>>
    {
        private readonly Expression condition;
        private readonly bool conditionFirst;

        private WhileStatement(Expression condition, bool conditionFirst)
        {
            this.condition = condition;
            this.conditionFirst = conditionFirst;
        }

        internal static WhileStatement While(Expression condition) => new(condition, true);

        internal static WhileStatement Until(Expression condition) => new(condition, false);

        WhileExpression ILexicalScope<WhileExpression, Action>.Build(Action scope)
        {
            scope();
            return new WhileExpression(condition, ContinueLabel, BreakLabel, conditionFirst) { Body = Build() };
        }

        WhileExpression ILexicalScope<WhileExpression, Action<LoopContext>>.Build(Action<LoopContext> scope)
        {
            var result = new WhileExpression(condition, ContinueLabel, BreakLabel, conditionFirst);
            using (var context = new LoopContext(result))
                scope(context);
            result.Body = Build();
            return result;
        }
    }
}