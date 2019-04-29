using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using UsingExpression = Linq.Expressions.UsingExpression;

    internal readonly struct UsingStatement : IStatement<UsingExpression, Action>, IStatement<UsingExpression, Action<ParameterExpression>>
    {
        private readonly Expression resource;

        internal UsingStatement(Expression resource) => this.resource = resource;

        UsingExpression IStatement<UsingExpression, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return new UsingExpression(resource, body.Build());
        }

        UsingExpression IStatement<UsingExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope, ILexicalScope body)
        {
            var result = new UsingExpression (resource);
            scope(result.Resource);
            result.Body = body.Build();
            return result;
        }
    }
}