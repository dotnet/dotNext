using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForEachExpression = Linq.Expressions.ForEachExpression;

    internal readonly struct ForEachStatement : IStatement<ForEachExpression, Action<MemberExpression>>, IStatement<ForEachExpression, Action<MemberExpression, LoopContext>>
    {
        private readonly Expression collection;

        internal ForEachStatement(Expression collection) => this.collection = collection;

        ForEachExpression IStatement<ForEachExpression, Action<MemberExpression>>.Build(Action<MemberExpression> scope, ILexicalScope body)
        {
            var result = new ForEachExpression(collection);
            scope(result.Element);
            result.Body = body.Build();
            return result;
        }

        ForEachExpression IStatement<ForEachExpression, Action<MemberExpression, LoopContext>>.Build(Action<MemberExpression, LoopContext> scope, ILexicalScope body)
        {
            var result = new ForEachExpression(collection);
            scope(result.Element, new LoopContext(result));
            result.Body = body.Build();
            return result;
        }
    }
}
