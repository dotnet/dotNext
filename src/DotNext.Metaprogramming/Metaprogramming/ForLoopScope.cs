using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class ForLoopScope: LoopScopeBase, IExpressionBuilder<BlockExpression>, ICompoundStatement<Action<ForLoopScope>>
    {
        private readonly Expression condition;
        private readonly ParameterExpression loopVar;
        private readonly BinaryExpression assignment;

        internal ForLoopScope(Expression initialization, Func<ParameterExpression, Expression> condition, LexicalScope parent = null)
            : base(parent)
        {
            loopVar = Expression.Variable(initialization.Type, "loop_var");
            assignment = Expression.Assign(loopVar, initialization);
            this.condition = condition(loopVar);
        }

        internal void ConstructBody(Action<ParameterExpression, LoopContext> body, Action<ParameterExpression> iteration)
        {
            using (var cookie = new LoopContext(this))
                body(loopVar, cookie);
            AddStatement(Expression.Label(ContinueLabel));
            iteration(loopVar);
        }

        internal void ConstructBody(Action<ParameterExpression> body, Action<ParameterExpression> iteration)
        {
            body(loopVar);
            AddStatement(Expression.Label(ContinueLabel));
            iteration(loopVar);
        }
        
        public new BlockExpression Build()
        {
            Expression body = Expression.Condition(condition, base.Build(), Expression.Goto(BreakLabel), typeof(void));
            body = Expression.Loop(body, BreakLabel);
            return Expression.Block(typeof(void), new[] { loopVar }, assignment, body);
        }

        void ICompoundStatement<Action<ForLoopScope>>.ConstructBody(Action<ForLoopScope> body) => body(this);
    }
}
