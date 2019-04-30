using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using WithExpression = Linq.Expressions.WithExpression;

    internal sealed class WithStatement : LexicalScope, ILexicalScope<WithExpression, Action<ParameterExpression>>
    {
        internal readonly struct Factory : IFactory<WithStatement>
        {
            private readonly Expression obj;

            internal Factory(Expression obj) => this.obj = obj;

            public WithStatement Create(LexicalScope parent) => new WithStatement(obj, parent);
        }

        private readonly Expression obj;

        private WithStatement(Expression obj, LexicalScope parent) : base(parent) => this.obj = obj;

        WithExpression ILexicalScope<WithExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new WithExpression(obj);
            scope(result.Variable);
            result.Body = Build();
            return result;
        }
    }
}