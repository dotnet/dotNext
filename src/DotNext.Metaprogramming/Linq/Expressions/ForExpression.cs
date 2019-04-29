using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using LoopContext = Metaprogramming.LoopContext;

    public sealed class ForExpression: Expression, ILoopExpression
    {
        internal readonly struct LoopIteration: ILazyExpression<Action<ParameterExpression>>
        {
            private readonly ForExpression loop;

            internal LoopIteration(ForExpression loop) => this.loop = loop;

            ref Expression ILazyExpression<Action<ParameterExpression>>.GetBody(Action<ParameterExpression> action)
            {
                action(loop.LoopVar);
                return ref loop.iteration;
            }
        }

        internal readonly struct LoopBody: ILazyExpression<Action<ParameterExpression>>, ILazyExpression<Action<ParameterExpression, LoopContext>>
        {
            private readonly ForExpression loop;

            internal LoopBody(ForExpression loop) => this.loop = loop;

            ref Expression ILazyExpression<Action<ParameterExpression>>.GetBody(Action<ParameterExpression> action)
            {
                action(loop.LoopVar);
                return ref loop.body;
            }

            ref Expression ILazyExpression<Action<ParameterExpression, LoopContext>>.GetBody(Action<ParameterExpression, LoopContext> action)
            {
                action(loop.LoopVar, new LoopContext(loop));
                return ref loop.body;
            }
        }

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
            LoopVar = Expression.Variable(initialization.Type, "loop_var");
            Test = builder.MakeCondition(LoopVar);
            ContinueLabel = Label(typeof(void), "continue");
            BreakLabel = Label(typeof(void), "break");
            body = builder.MakeBody(LoopVar);
            iteration = builder.MakeIteration(LoopVar);
        }

        internal LoopBody GetBody() => new LoopBody(this);

        internal LoopIteration GetIteration() => new LoopIteration(this);

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