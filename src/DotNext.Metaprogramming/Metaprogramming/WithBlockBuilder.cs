using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class WithBlockBuilder: ScopeBuilder, IExpressionBuilder<Expression>
    {
        private readonly ParameterExpression scopeVar;

        internal WithBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                scopeVar = variable;
            else
            {
                scopeVar = DeclareVariable(expression.Type, NextName("block_var_"));
                Assign(ScopeVar, expression);
            }
        }

        public UniversalExpression ScopeVar { get; }

        Expression IExpressionBuilder<Expression>.Build() => Build();
    }
}