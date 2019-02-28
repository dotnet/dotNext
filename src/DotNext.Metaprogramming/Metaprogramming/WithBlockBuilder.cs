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

        internal WithBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            if(expression is ParameterExpression variable)
                scopeVar = variable;
            else
            {
                scopeVar = DeclareVariable(expression.Type, NextName("block_var_"));
                Assign(scopeVar, expression);
            }
        }

        /// <summary>
        /// Represents implictly referenced value in this scope.
        /// </summary>
        public UniversalExpression ScopeVar => scopeVar;

        Expression IExpressionBuilder<Expression>.Build() => Build();
    }
}