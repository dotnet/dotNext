using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using WithExpression = Linq.Expressions.WithExpression;

    internal readonly struct WithStatement : IStatement<WithExpression, Action<ParameterExpression>>
    {
        private readonly Expression obj;

        internal WithStatement(Expression obj) => this.obj = obj;

        WithExpression IStatement<WithExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope, ILexicalScope body)
        {
            var result = new WithExpression(obj);
            scope(result.Variable);
            result.Body = body.Build();
            return result;
        }
    }
}