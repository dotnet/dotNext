using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class ForLoopBuilder: LoopBuilderBase, IExpressionBuilder<BlockExpression>
    {
        private readonly Expression condition;
        internal readonly ParameterExpression LoopVar;
        private readonly BinaryExpression assignment;
        private bool continueLabelInstalled;
        
        internal ForLoopBuilder(Expression initialization, Func<ParameterExpression, Expression> condition, LexicalScope parent = null)
            : base(parent)
        {
            LoopVar = Expression.Variable(initialization.Type, "loop_var");
            assignment = Expression.Assign(LoopVar, initialization);
            this.condition = condition(LoopVar);
        }

        internal void StartIterationCode()
        {
            if(continueLabelInstalled)
                throw new InvalidOperationException();
            AddStatement(Expression.Label(continueLabel));
        }
        
        public new BlockExpression Build()
        {
            Expression body = Expression.Condition(condition, base.Build(), Expression.Goto(breakLabel), typeof(void));
            body = continueLabelInstalled ? Expression.Loop(body, breakLabel) : Expression.Loop(body, breakLabel, continueLabel);
            return Expression.Block(typeof(void), new[] { LoopVar }, assignment, body);
        }
    }
}
