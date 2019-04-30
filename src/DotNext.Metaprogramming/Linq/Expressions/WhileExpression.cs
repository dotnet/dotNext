using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class WhileExpression: Expression, ILoopLabels
    {
        public delegate Expression Statement(LabelTarget continueLabel, LabelTarget breakLabel);

        private static Expression MakeBody(Statement statement, out LabelTarget continueLabel, out LabelTarget breakLabel)
            => statement(continueLabel = Label(typeof(void), "continue"), breakLabel = Label(typeof(void), "break"));

        private readonly bool conditionFirst;
        private Expression body;

        internal WhileExpression(Expression test, LabelTarget continueLabel, LabelTarget breakLabel, bool checkConditionFirst)
        {
            conditionFirst = checkConditionFirst;
            Test = test;
            ContinueLabel = continueLabel ?? Label(typeof(void), "continue");
            BreakLabel = breakLabel ?? Label(typeof(void), "break");
        }

        public WhileExpression(Expression test, Statement body, bool checkConditionFirst = true)
            : this(test, null, null, checkConditionFirst)
        {
            this.body = body(ContinueLabel, BreakLabel);
        }

        public WhileExpression(Expression test, Expression body, bool checkConditionFirst = true)
            : this(test, null, null, checkConditionFirst)
        {
        }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Test { get; }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public WhileExpression Update(Expression body) => new WhileExpression(Test, body, conditionFirst);

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