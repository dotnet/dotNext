using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForExpression = Linq.Expressions.ForExpression;

    internal sealed class ForLoopScope : LexicalScope, IExpressionBuilder<ForExpression>
    {
        private readonly Expression initialization;
        private readonly ForExpression.Condition condition;
        private readonly Action<ParameterExpression> iteration;

        private readonly MulticastDelegate action;

        internal ForLoopScope(Expression initialization, ForExpression.Condition condition, Action<ParameterExpression> iteration, Action<ParameterExpression> action, LexicalScope parent)
            : base(parent)
        {
            this.initialization = initialization;
            this.condition = condition;
            this.iteration = iteration;
            this.action = action;
        }

        internal ForLoopScope(Expression initialization, ForExpression.Condition condition, Action<ParameterExpression> iteration, Action<ParameterExpression, LoopContext> action, LexicalScope parent)
            : base(parent)
        {
            this.initialization = initialization;
            this.condition = condition;
            this.iteration = iteration;
            this.action = action;
        }

        private Expression BuildBody(ParameterExpression loopVar, LabelTarget continueLabel, LabelTarget breakLabel)
        {
            switch(this.action)
            {
                case Action<ParameterExpression> action: 
                    action(loopVar); 
                    break;
                case Action<ParameterExpression, LoopContext> action: 
                    action(loopVar, new LoopContext(continueLabel, breakLabel)); 
                    break;
            }
            AddStatement(Expression.Goto(continueLabel));
            iteration(loopVar);
            return base.Build();
        }

        public new ForExpression Build() => new ForExpression(initialization, condition, BuildBody);
    }
}
