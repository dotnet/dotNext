using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class WithBlockBuilder: ScopeBuilder, IExpressionBuilder<Expression>
    {
        internal readonly ParameterExpression Variable;
        private readonly BinaryExpression assignment;

        internal WithBlockBuilder(Expression expression, LexicalScope parent = null)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                Variable = variable;
            else
            {
                Variable = Expression.Variable(expression.Type, "scopeVar");
                assignment = Expression.Assign(Variable, expression);
            }
        }

        public new Expression Build()
        {
            var body = base.Build();
            if (!(assignment is null))
                body = Expression.Block(typeof(void), new[] { Variable }, assignment, body);
            return body;
        }
    }
}