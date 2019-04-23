using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class WithBlockScope: LexicalScope, IExpressionBuilder<Expression>, ICompoundStatement<Action<ParameterExpression>>
    {
        private readonly ParameterExpression variable;
        private readonly BinaryExpression assignment;

        internal WithBlockScope(Expression expression, LexicalScope parent = null)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                this.variable = variable;
            else
            {
                this.variable = Expression.Variable(expression.Type, "scopeVar");
                assignment = Expression.Assign(this.variable, expression);
            }
        }

        void ICompoundStatement<Action<ParameterExpression>>.ConstructBody(Action<ParameterExpression> body) => body(variable);

        public new Expression Build()
        {
            var body = base.Build();
            if (!(assignment is null))
                body = Expression.Block(typeof(void), new[] { variable }, assignment, body);
            return body;
        }
    }
}