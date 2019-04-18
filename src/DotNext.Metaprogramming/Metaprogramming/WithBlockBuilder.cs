using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents <see langword="With"/> statement builder. 
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
    public sealed class WithBlockBuilder: ScopeBuilder, IExpressionBuilder<Expression>
    {
        private readonly ParameterExpression scopeVar;
        private readonly BinaryExpression assignment;

        internal WithBlockBuilder(Expression expression, CompoundStatementBuilder parent = null)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                scopeVar = variable;
            else
            {
                scopeVar = Expression.Variable(expression.Type, "scopeVar");
                assignment = Expression.Assign(scopeVar, expression);
            }
        }

        /// <summary>
        /// Represents implictly referenced value in this scope.
        /// </summary>
        public UniversalExpression ScopeVar => scopeVar;

        internal override Expression Build() => Build<Expression, WithBlockBuilder>(this);

        Expression IExpressionBuilder<Expression>.Build()
        {
            var body = base.Build();
            if (!(assignment is null))
                body = Expression.Block(typeof(void), new[] { scopeVar }, assignment, body);
            return body;
        }
    }
}