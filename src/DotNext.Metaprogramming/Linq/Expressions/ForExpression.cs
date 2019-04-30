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

        private Expression body;

        internal ForExpression(Expression initialization, Func<ParameterExpression, Expression> condition)
        {
            Initialization = initialization;
            LoopVar = Variable(initialization.Type, "loop_var");
            Test = condition(LoopVar);
            ContinueLabel = Label(typeof(void), "continue");
            BreakLabel = Label(typeof(void), "break");
        }

        internal ForExpression(Expression initialization, IBuilder builder)
            : this(initialization, builder.MakeCondition)
        {
            Body = builder.MakeBody(LoopVar).AddPrologue(false, Continue(ContinueLabel), builder.MakeIteration(LoopVar));
        }

        public Expression Test { get; }

        public Expression Initialization { get; }

        public ParameterExpression LoopVar { get; }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            Expression body = Condition(Test, Body, Goto(BreakLabel), typeof(void));
            body = Loop(body, BreakLabel);
            return Block(typeof(void), Sequence.Singleton(LoopVar), Assign(LoopVar, Initialization), body);
        }
    }
}