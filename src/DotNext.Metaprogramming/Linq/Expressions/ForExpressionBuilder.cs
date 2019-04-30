using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class ForExpressionBuilder : ExpressionBuilder<ForExpression>, ForExpression.IBuilder
    {
        private readonly Expression initialization;
        private Func<ParameterExpression, Expression> condition, iteration, body;

        internal ForExpressionBuilder(Expression initialization) => this.initialization = initialization;

        public ForExpressionBuilder While(Func<ParameterExpression, Expression> condition)
        {
            VerifyCaller();
            this.condition = condition;
            return this;
        }

        public ForExpressionBuilder Do(Func<ParameterExpression, Expression> body)
        {
            VerifyCaller();
            this.body = body;
            return this;
        }

        public ForExpressionBuilder Iterate(Func<ParameterExpression, Expression> iteration)
        {
            VerifyCaller();
            this.iteration = iteration;
            return this;
        }

        Expression ForExpression.IBuilder.MakeCondition(ParameterExpression loopVar) => condition(loopVar);

        Expression ForExpression.IBuilder.MakeIteration(ParameterExpression loopVar) => iteration(loopVar);

        Expression ForExpression.IBuilder.MakeBody(ParameterExpression loopVar) => body(loopVar);

        private protected override ForExpression Build() => new ForExpression(initialization, this);
    }
}