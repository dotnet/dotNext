using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class ForExpression: Expression, ILoopExpression
    {
        internal interface IBuilder
        {
            Expression MakeCondition(ParameterExpression loopVar);
            Expression MakeIteration(ParameterExpression loopVar);
            Expression MakeBody(ParameterExpression loopVar);
        }

        private Expression iteration, body;

        internal ForExpression(Expression initialization, IBuilder builder)
        {
            Initialization = initialization;
            LoopVar = Variable(initialization.Type, "loop_var");
            Test = builder.MakeCondition(LoopVar);
            ContinueLabel = Label(typeof(void), "continue");
            BreakLabel = Label(typeof(void), "break");
            body = builder.MakeBody(LoopVar);
            iteration = builder.MakeIteration(LoopVar);
        }

        public Expression Iteration => iteration;

        public Expression Test { get; }

        public Expression Initialization { get; }

        public ParameterExpression LoopVar { get; }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Body => body;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            Expression body = Condition(Test, Body.AddPrologue(false, Continue(ContinueLabel), Iteration), Goto(BreakLabel), typeof(void));
            body = Loop(body, BreakLabel);
            return Block(typeof(void), Sequence.Singleton(LoopVar), Assign(LoopVar, Initialization), body);
        }
    }
}