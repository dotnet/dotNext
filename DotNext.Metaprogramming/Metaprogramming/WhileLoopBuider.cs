using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public sealed class WhileLoopBuider: LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        private readonly Expression test;
        private readonly bool conditionFirst;

        internal WhileLoopBuider(Expression test, ExpressionBuilder parent, bool checkConditionFirst)
            : base(parent)
        {
            this.test = test;
            conditionFirst = checkConditionFirst;
        }

        internal override Expression Build() => this.Upcast<IExpressionBuilder<LoopExpression>, WhileLoopBuider>().Build();

        LoopExpression IExpressionBuilder<LoopExpression>.Build()
        {
            Expression loopBody;
            LoopExpression loopExpr;
            if(conditionFirst)
            {
                loopBody = test.Condition(base.Build(), breakLabel.Goto());
                loopExpr = loopBody.Loop(breakLabel, continueLabel);
            }
            else
            {
                var condition = test.Condition(ifFalse: Expression.Goto(breakLabel));
                loopBody = Expression.Block(typeof(void), base.Build(), continueLabel.LandingSite(), condition);
                loopExpr = loopBody.Loop(breakLabel);
            }
            return loopExpr;
        }
    }
}