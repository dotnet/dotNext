using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents <see langword="for"/> loop statement builder.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
    public sealed class ForLoopBuilder: LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        private readonly Expression condition;
        private readonly ParameterExpression loopVar;
        private bool continueLabelInstalled;
        
        internal ForLoopBuilder(Expression initialization, Func<UniversalExpression, Expression> condition, CompoundStatementBuilder parent)
            : base(parent)
        {
            loopVar = parent.DeclareVariable(initialization.Type, NextName("loop_var"));
            parent.Assign(loopVar, initialization);
            this.condition = condition(LoopVar);
        }

        /// <summary>
        /// Starts iterator block of code which will be executed in the end of every iteration.
        /// </summary>
        public void StartIteratorBlock()
        {
            Label(continueLabel);
            continueLabelInstalled = true;
        }

        /// <summary>
        /// Gets declared loop variable.
        /// </summary>
        public UniversalExpression LoopVar => loopVar;

        internal override Expression Build() => Build<LoopExpression, ForLoopBuilder>(this);

        LoopExpression IExpressionBuilder<LoopExpression>.Build()
        {
            var body = Expression.Condition(condition, base.Build(), Expression.Goto(breakLabel), typeof(void));
            return continueLabelInstalled ? Expression.Loop(body, breakLabel) : Expression.Loop(body, breakLabel, continueLabel);
        }
    }
}
