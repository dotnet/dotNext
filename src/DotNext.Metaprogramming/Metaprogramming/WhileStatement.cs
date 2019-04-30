using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using WhileExpression = Linq.Expressions.WhileExpression;

    internal sealed class WhileStatement : LoopLexicalScope, ILexicalScope<WhileExpression, Action>, ILexicalScope<WhileExpression, Action<LoopContext>>
    {
        internal readonly struct Factory : IFactory<WhileStatement>
        {
            private readonly Expression condition;
            private readonly bool conditionFirst;

            internal Factory(Expression condition, bool conditionFirst)
            {
                this.condition = condition;
                this.conditionFirst = conditionFirst;
            }

            public WhileStatement Create(LexicalScope parent) => new WhileStatement(condition, conditionFirst, parent);
        }

        private readonly Expression condition;
        private readonly bool conditionFirst;

        private WhileStatement(Expression condition, bool conditionFirst, LexicalScope parent)
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
            var result = new WhileExpression(condition, ContinueLabel, BreakLabel, conditionFirst);
            using(var context = new LoopContext(result))
                scope(context);
            result.Body = Build();
            return result;
        }
    }
}