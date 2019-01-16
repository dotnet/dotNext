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
                loopBody = Expression.Condition(test, this.Upcast<ScopeBuilder, WhileLoopBuider>().BuildExpression(), Expression.Goto(breakLabel), typeof(void));
                loopExpr = Expression.Loop(loopBody, breakLabel, continueLabel);
            }
            else
            {
                var condition = Expression.Condition(test, Expression.Default(typeof(void)), Expression.Goto(breakLabel), typeof(void));
                loopBody = Expression.Block(typeof(void), this.Upcast<ScopeBuilder, WhileLoopBuider>().BuildExpression(), Expression.Label(continueLabel), condition);
                loopExpr = Expression.Loop(loopBody, breakLabel);
            }
            return loopExpr;
        }
    }
}