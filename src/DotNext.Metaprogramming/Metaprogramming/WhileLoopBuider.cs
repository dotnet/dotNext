using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents <see langword="while"/> loop builder.
    /// </summary>
    public sealed class WhileLoopBuider: LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        private readonly Expression test;
        private readonly bool conditionFirst;

        internal WhileLoopBuider(Expression test, CompoundStatementBuilder parent, bool checkConditionFirst)
            : base(parent)
        {
            this.test = test;
            conditionFirst = checkConditionFirst;
        }

        internal override Expression Build() => ((IExpressionBuilder<LoopExpression>)this).Build();

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