using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    public sealed class ForExpressionBuilder : ExpressionBuilder<ForExpression>, ForExpression.IBuilder
    {
        private readonly Expression initialization;
        private Func<ParameterExpression, Expression> condition;

        public ForExpressionBuilder While(Func<ParameterExpression, Expression> condition)
        {
            this.condition = condition;
            return this;
        }

        Expression ForExpression.IBuilder.MakeCondition(ParameterExpression loopVar) => condition(loopVar);

        private protected override ForExpression Build() => new ForExpression(initialization, this);
    }
}