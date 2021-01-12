using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using UsingExpression = Linq.Expressions.UsingExpression;

    internal sealed class UsingStatement : Statement, ILexicalScope<UsingExpression, Action>, ILexicalScope<UsingExpression, Action<ParameterExpression>>
    {
        private readonly Expression resource;

        internal UsingStatement(Expression resource) => this.resource = resource;

        UsingExpression ILexicalScope<UsingExpression, Action>.Build(Action scope)
        {
            scope();
            return new UsingExpression(resource) { Body = Build() };
        }

        UsingExpression ILexicalScope<UsingExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new UsingExpression(resource);
            scope(result.Resource);
            result.Body = Build();
            return result;
        }
    }
}