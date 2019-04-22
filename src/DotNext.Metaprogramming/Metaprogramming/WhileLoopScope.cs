using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents <see langword="while"/> loop builder.
    /// </summary>
    internal sealed class WhileLoopScope: LoopBuilderBase, IExpressionBuilder<LoopExpression>, ICompoundStatement<Action<LoopCookie>>
    {
        private readonly Expression test;
        private readonly bool conditionFirst;

        internal WhileLoopScope(Expression test, LexicalScope parent, bool checkConditionFirst)
            : base(parent)
        {
            this.test = test;
            conditionFirst = checkConditionFirst;
        }

        public new LoopExpression Build()
        {
            Expression loopBody;
            LoopExpression loopExpr;
            if(conditionFirst)
            {
                loopBody = test.Condition(base.Build(), BreakLabel.Goto());
                loopExpr = loopBody.Loop(BreakLabel, ContinueLabel);
            }
            else
            {
                var condition = test.Condition(ifFalse: Expression.Goto(BreakLabel));
                loopBody = Expression.Block(typeof(void), base.Build(), ContinueLabel.LandingSite(), condition);
                loopExpr = loopBody.Loop(BreakLabel);
            }
            return loopExpr;
        }

        void ICompoundStatement<Action<LoopCookie>>.ConstructBody(Action<LoopCookie> body)
        {
            using (var cookie = new LoopCookie(this))
                body(cookie);
        }
    }
}