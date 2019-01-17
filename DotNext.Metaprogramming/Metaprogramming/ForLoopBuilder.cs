using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public sealed class ForLoopBuilder: LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        private static long counter;

        private readonly Expression condition;
        private bool continueLabelInstalled;
        
        internal ForLoopBuilder(Expression initialization, Func<ExpressionView, Expression> condition, ExpressionBuilder parent)
            : base(parent)
        {
            LoopVar = parent.DeclareVariable(initialization.Type, "for_loop_var" + counter.IncrementAndGet());
            parent.Assign(LoopVar, initialization);
            this.condition = condition(LoopVar);
        }

        public void StartIteratorBlock()
        {
            Label(continueLabel);
            continueLabelInstalled = true;
        }

        public ParameterExpression LoopVar { get; }

        internal override Expression Build() => this.Upcast<IExpressionBuilder<LoopExpression>, ForLoopBuilder>().Build();

        LoopExpression IExpressionBuilder<LoopExpression>.Build()
        {
            var body = Expression.Condition(condition, base.Build(), Expression.Goto(breakLabel), typeof(void));
            return continueLabelInstalled ? Expression.Loop(body, breakLabel) : Expression.Loop(body, breakLabel, continueLabel);
        }
    }
}
