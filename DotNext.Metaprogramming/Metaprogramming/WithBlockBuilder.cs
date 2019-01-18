using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class WithBlockBuilder: ExpressionBuilder, IExpressionBuilder<Expression>
    {
        internal WithBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                ScopeVar = variable;
            else
            {
                ScopeVar = DeclareVariable(expression.Type, NextName("block_var_"));
                Assign(ScopeVar, expression);
            }
        }

        public UniversalExpression ScopeVar { get; }

        Expression IExpressionBuilder<Expression>.Build() => Build();
    }
}