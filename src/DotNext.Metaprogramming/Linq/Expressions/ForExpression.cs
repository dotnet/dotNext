using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class ForExpression: Expression, ILoopLabels
    {
        internal interface IBuilder : ILoopLabels
        {
            Expression MakeCondition(ParameterExpression loopVar);
            Expression MakeIteration(ParameterExpression loopVar);
            Expression MakeBody(ParameterExpression loopVar);
        }

        public sealed class LoopBuilder : IBuilder, IExpressionBuilder<ForExpression>
        {
            private readonly LabelTarget continueLabel, breakLabel;
            private readonly Expression initialization;
            private Func<ParameterExpression, Expression> condition, iteration, body;

            internal LoopBuilder(Expression initialization)
            {
                this.initialization = initialization;
                breakLabel = Expression.Label(typeof(void), "break");
                continueLabel = Expression.Label(typeof(void), "continueLabel");
            }

            public LoopBuilder While(Func<ParameterExpression, Expression> condition)
            {
                this.condition = condition;
                return this;
            }

            public LoopBuilder Do(Func<ParameterExpression, Expression> body)
            {
                this.body = body;
                return this;
            }

            public LoopBuilder Iteration(Func<ParameterExpression, Expression> iteration)
            {
                this.iteration = iteration;
                return this;
            }

            LabelTarget ILoopLabels.BreakLabel => breakLabel;
            LabelTarget ILoopLabels.ContinueLabel => continueLabel;
            
            Expression ForExpression.IBuilder.MakeCondition(ParameterExpression loopVar) => condition(loopVar);

            Expression ForExpression.IBuilder.MakeIteration(ParameterExpression loopVar) => iteration(loopVar);

            Expression ForExpression.IBuilder.MakeBody(ParameterExpression loopVar) => body(loopVar);

            public ForExpression Build() => new ForExpression(initialization, this);
        }

        private Expression body;

        internal ForExpression(Expression initialization, LabelTarget continueLabel, LabelTarget breakLabel, Func<ParameterExpression, Expression> condition)
        {
            Initialization = initialization;
            LoopVar = Variable(initialization.Type, "loop_var");
            Test = condition(LoopVar);
            ContinueLabel = continueLabel ?? Label(typeof(void), "continue");
            BreakLabel = breakLabel ?? Label(typeof(void), "break");
        }

        internal ForExpression(Expression initialization, IBuilder builder)
            : this(initialization, builder.ContinueLabel, builder.BreakLabel, builder.MakeCondition)
        {
            body = builder.MakeBody(LoopVar).AddPrologue(false, Continue(ContinueLabel), builder.MakeIteration(LoopVar));
        }

        public static LoopBuilder Builder(Expression initialization) => new LoopBuilder(initialization);

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