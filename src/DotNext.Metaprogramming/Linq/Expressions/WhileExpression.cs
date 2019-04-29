using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class WhileExpression: Expression, ILazyExpression<Action>, ILoopExpression
    {
        private readonly bool conditionFirst;
        private Expression body;

        public WhileExpression(Expression test, Expression body = null, bool checkConditionFirst = true)
        {
            Test = test;
            this.body = body ?? Empty();
            BreakLabel = Label(typeof(void), "break");
            ContinueLabel = Label(typeof(void), "continue");
            conditionFirst = checkConditionFirst;
        }

        ref Expression ILazyExpression<Action>.GetBody(Action action)
        {
            action();
            return ref body;
        }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Test { get; }

        public Expression Body => body;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public WhileExpression Update(Expression body) => new WhileExpression(Test, body, conditionFirst);

        public WhileExpression Update(Expression test, Expression body) => new WhileExpression(test, body, conditionFirst);

        public override Expression Reduce()
        {
            Expression loopBody;
            LoopExpression loopExpr;
            if (conditionFirst)
            {
                loopBody = Test.Condition(Body, Goto(BreakLabel));
                loopExpr = Loop(loopBody, BreakLabel, ContinueLabel);
            }
            else
            {
                var condition = Condition(Test, Empty(), Goto(BreakLabel));
                loopBody = Block(typeof(void), Body, Label(ContinueLabel), condition);
                loopExpr = Loop(loopBody, BreakLabel);
            }
            return loopExpr;
        }
    }
}