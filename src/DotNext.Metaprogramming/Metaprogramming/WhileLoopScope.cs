using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class WhileLoopScope : LoopScopeBase, IExpressionBuilder<LoopExpression>, ICompoundStatement<Action<LoopContext>>
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
            if (conditionFirst)
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

        void ICompoundStatement<Action<LoopContext>>.ConstructBody(Action<LoopContext> body)
        {
            using (var context = new LoopContext(this))
                body(context);
        }
    }
}