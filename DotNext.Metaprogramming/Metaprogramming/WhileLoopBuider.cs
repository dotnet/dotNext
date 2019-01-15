using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class WhileLoopBuider: ScopeBuilder
    {
        private readonly ScopeBuilder parentScope;
        private readonly Expression test;
        private readonly bool conditionFirst;
        private readonly LabelTarget breakLabel;
        private readonly LabelTarget continueLabel;

        internal WhileLoopBuider(Expression test, ScopeBuilder parent, bool checkConditionFirst)
        {
            this.test = test;
            this.parentScope = parent;
            conditionFirst = checkConditionFirst;
            breakLabel = Expression.Label();
            continueLabel = Expression.Label();
            if(checkConditionFirst)
                LabelStatement(continueLabel);
        }

        public LabelExpression Continue() => LabelStatement(continueLabel);

        public LabelExpression Break() => LabelStatement(breakLabel);

        internal new LoopExpression BuildExpression()
        {
            LoopExpression loopExpr;
            if(conditionFirst)
            {
                loopExpr = Expression.Loop(Expression.Condition(test, base.BuildExpression(), Expression.Goto(breakLabel)));
            }
            else
            {
                LabelStatement(continueLabel);
                AddStatement(Expression.Condition(test, null, Expression.Goto(breakLabel)));
                loopExpr = Expression.Loop(base.BuildExpression(), breakLabel, continueLabel);
            }
            parentScope.AddStatement(loopExpr);
            parentScope.LabelStatement(breakLabel);
            return loopExpr;
        }
    }
}