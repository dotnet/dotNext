using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using UsingExpression = Linq.Expressions.UsingExpression;

    internal sealed class AwaitUsingStatement : Statement, ILexicalScope<UsingExpression, Action>, ILexicalScope<UsingExpression, Action<ParameterExpression>>
    {
        private readonly Expression resource;
        private readonly bool configureAwait;

        internal AwaitUsingStatement(Expression resource, bool configureAwait)
        {
            this.resource = resource;
            this.configureAwait = configureAwait;
        }

        UsingExpression ILexicalScope<UsingExpression, Action>.Build(Action scope)
        {
            scope();
            return new UsingExpression(resource, configureAwait) { Body = Build() };
        }

        UsingExpression ILexicalScope<UsingExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new UsingExpression(resource, configureAwait);
            scope(result.Resource);
            result.Body = Build();
            return result;
        }
    }
}