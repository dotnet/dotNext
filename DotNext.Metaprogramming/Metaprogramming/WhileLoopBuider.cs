using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public sealed class WhileLoopBuider: LoopBuilder
    {
        private readonly Expression test;
        private readonly bool conditionFirst;

        internal WhileLoopBuider(Expression test, ScopeBuilder parent, bool checkConditionFirst)
            : base(parent)
        {
            this.test = test;
            conditionFirst = checkConditionFirst;
        }

        internal new LoopExpression BuildExpression()
        {
            Expression loopBody;
            LoopExpression loopExpr;
            if(conditionFirst)
            {
                loopBody = test.Condition(this.Upcast<ScopeBuilder, WhileLoopBuider>().BuildExpression(), breakLabel.Goto());
                loopExpr = loopBody.Loop(breakLabel, continueLabel);
            }
            else
            {
                var condition = test.Condition(ifFalse: Expression.Goto(breakLabel));
                loopBody = Expression.Block(typeof(void), this.Upcast<ScopeBuilder, WhileLoopBuider>().BuildExpression(), continueLabel.LandingSite(), condition);
                loopExpr = loopBody.Loop(breakLabel);
            }
            return loopExpr;
        }
    }
}