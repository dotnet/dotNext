using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForEachExpression = Linq.Expressions.ForEachExpression;

    internal sealed class ForEachStatement : LexicalScope, ILexicalScope<ForEachExpression, Action<MemberExpression>>, ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>
    {
        private readonly Expression collection;

        internal ForEachStatement(Expression collection, LexicalScope parent) : base(parent) => this.collection = collection;

        ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression>>.Build(Action<MemberExpression> scope)
        {
            var result = new ForEachExpression(collection);
            scope(result.Element);
            result.Body = Build();
            return result;
        }

        ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>.Build(Action<MemberExpression, LoopContext> scope)
        {
            var result = new ForEachExpression(collection);
            scope(result.Element, new LoopContext(result));
            result.Body = Build();
            return result;
        }
    }
}
