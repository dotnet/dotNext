using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    internal sealed class WhileLoopScope : LoopScopeBase, IExpressionBuilder<WhileExpression>, ICompoundStatement<Action<LoopContext>>
    {
        private readonly WhileExpression expression;

        internal WhileLoopScope(Expression test, LexicalScope parent, bool checkConditionFirst)
            : base(parent) => expression = new WhileExpression(test, Expression.Empty(), checkConditionFirst);

        public new WhileExpression Build() => expression.Update(base.Build());

        internal override LabelTarget BreakLabel => expression.BreakLabel;
        internal override LabelTarget ContinueLabel => expression.ContinueLabel;

        void ICompoundStatement<Action<LoopContext>>.ConstructBody(Action<LoopContext> body)
        {
            using (var context = new LoopContext(this))
                body(context);
        }
    }
}